using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// <summary>
    /// 绿·慢速 AI（2026-11-14 按新预组 v8 重写·用户口径）：
    /// 前期：靠「减费」滚雪球——小精灵（登场给角色 2 甲；本回合第一张再给手牌 −2 绿）起手，
    ///       萌芽/沃土（成长 + 减费门）连打，把地铺起来；三张抹除与两种大生物是核心组件，
    ///       抽到**留一张在手里**，不轻易当土地烧掉。
    /// 中期：优先打「对手卡最多的那一种」抹除（生物多→凋零 / 结界多→溃散 / 地多→枯竭）。
    /// 两种大生物控场：古木守卫（2/8 守护+帷幕）挡线，暗影豹（9/9 攻守+狩猎+吸血）终结。
    /// 后期：牺牲 + 回收轮转——成长会随机把牌拉进地牌池，池里的核心组件用技能「牺牲」送墓，
    ///       再用归根把墓地的核心组件捞回手；多余的地也可以祭掉换彩元素。
    /// 卡组（60）：小精灵×10 / 萌芽×10 / 沃土×10 / 归根×10 / 凋零×2 / 溃散×2 / 枯竭×2 /
    ///            虚化×4 / 古木守卫×5 / 暗影豹×5；技能=牺牲。
    /// </summary>
    public sealed class GreenRampStrategy : AiStrategy
    {
        private const string NameSpirit = "小精灵";   // 1/1 无攻守；登场己方角色 +2 甲；首卡 → 手牌 −2 绿
        private const string NameSprout = "萌芽";     // 成长 1 + 首卡减费
        private const string NameBloom = "沃土";      // 成长 2 + 地≥9 减费
        private const string NameReturn = "归根";     // 回收 3 张
        private const string NameEraseCreature = "凋零"; // 抹除对方生物
        private const string NameEraseEnchant = "溃散";  // 抹除对方结界
        private const string NameEraseLand = "枯竭";     // 抹除对方元素池地牌
        private const string NameWall = "古木守卫";   // 2/8 守护+帷幕
        private const string NamePanther = "暗影豹";  // 9/9 攻守 狩猎+吸血
        private const string NameVeil = "虚化";       // 赋予无实体 + 地≥9 护甲

        /// <summary>核心组件（抹除三种 + 两种大生物）——抽到留一张在手里，不轻易当地。</summary>
        private static readonly HashSet<string> CoreNames = new HashSet<string>
        {
            NameEraseCreature, NameEraseEnchant, NameEraseLand, NameWall, NamePanther,
        };

        private static readonly HashSet<string> EraseNames = new HashSet<string>
        {
            NameEraseCreature, NameEraseEnchant, NameEraseLand,
        };

        /// <summary>成长引擎：前期能打就打。</summary>
        private static readonly HashSet<string> RampNames = new HashSet<string> { NameSprout, NameBloom };

        /// <summary>地牌门槛（门=操控地数量≥9；9 地后减费/护甲门开启）。</summary>
        private const int GateLands = 9;
        /// <summary>暗影豹 6 红费——单绿套要靠牺牲彩元素补，钱不够先留手。</summary>
        private const int PantherMinTotal = 12;

        public override string Key => "green";
        public override string DisplayName => "绿慢速";

        // ======================================== 技能：牺牲 ========================================

        /// <summary>技能=牺牲（自选**任意数量**己方单位/地牌送墓，每张 +1 临时彩元素）。
        /// 两种时机：① 地牌池里躺着核心组件（被成长随机拉进去的）→ 全祭掉救人；
        /// ② 手里有一张「差几点就能打」的牌 → 按差额祭掉对应张数的废料（1/1 小精灵、多余地）。
        /// 典型开局：放 1 地 → 小精灵 → 手牌成长拿到 −2 绿 → 祭小精灵换 1 彩 → 成长落地。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
        {
            if (!base.WantHeroSkill(core, me)) return false;
            if (CoreStuckInPool(core, me).Count > 0) return true;   // 救核心
            int need = ShortfallNeed(core, me);
            return need > 0 && FodderCount(core, me) >= need;
        }

        /// <summary>救核心要先于出牌（否则核心地牌会被当成普通地继续产色/被忽略）。</summary>
        public override bool SkillBeforeCards(GameCore core, Player me)
            => CoreStuckInPool(core, me).Count > 0;

        // ======================================== 出牌优先级 ========================================

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            string name = NameOf(card);
            if (name == null) return totalCost(card);

            if (RampNames.Contains(name))
            {
                // 前期核心：成长优先（越高越好），9 地后降权（门已开，优先抹除/大哥）
                int lands = TotalLands(core, me);
                float ramp = lands < GateLands ? 5200f : 2200f;
                // 首卡/减费链：本回合还没出牌的萌芽/沃土最赚（首卡门 +2 层减费）
                if (IsFirstCardThisTurn(core, me)) ramp += 800f;
                return ramp;
            }

            if (name == NameSpirit)
            {
                // 小精灵：早期给角色叠甲 + 首卡减费，最优先铺；中期以后降为填线
                int lands = TotalLands(core, me);
                float s = lands < GateLands ? 4200f : 900f;
                if (IsFirstCardThisTurn(core, me)) s += 900f;
                return s;
            }

            if (EraseNames.Contains(name))
            {
                // 中期：只打「对手最多类型」对应的那一种抹除；没对应目标=留手
                return MatchesOpponentMost(name, core, me) ? 3600f : 200f;
            }

            if (name == NameReturn)
            {
                int coreGy = CoreInGraveyard(core, me);
                return coreGy > 0 ? 3200f : (GyCount(core, me) >= 3 ? 1200f : 300f);
            }

            if (name == NameWall) return EnemyBodies(core, me) > 0 ? 3000f : 1800f;
            if (name == NamePanther) return TotalLands(core, me) >= GateLands ? 3400f : 1600f;
            if (name == NameVeil) return EnemyBodies(core, me) > 0 ? 1400f : 600f;

            return totalCost(card);
        }

        /// <summary>攒牌闸（用户口径「核心组件抽到留一张在手里」）：
        /// 抹除——对手没有对应类型的目标时留手，不空放；
        /// 暗影豹——地/钱没到位（<见 PantherMinTotal）先留手，别当费牌打掉；
        /// 归根——墓地没有核心组件、且墓地不足 3 张时留手。</summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
            => ShouldHoldStatic(core, me, card);

        // ======================================== 地牌 ========================================

        /// <summary>铺地口径（2026-11-14 重写）：**核心组件（抹除/两种大生物）永不当土地**——
        /// 「抽到留一张在手里」；虚化也不当土地。其余卡在起步缺地（&lt;3 块）时可铺，
        /// 3 块之后只在多册（&gt;1 张）时才铺，保证手里始终有牌可打。</summary>
        public override bool ShouldLand(GameCore core, Player me, Card card)
        {
            string name = NameOf(card);
            if (name == null) return true;
            if (CoreNames.Contains(name)) return false;   // 抹除/大生物留手
            if (name == NameVeil) return false;           // 虚化留手
            int lands = TotalLands(core, me);
            if (lands < 3) return true;                   // 起步缺地：除核心外都可铺
            return HandCopies(core, me, name) > 1;        // 3 地后：留 1 张，多册才铺
        }

        /// <summary>主动铺地（2026-11-14 重写）：这套牌要 9 地开门（减费/护甲），所以付得起也要铺——
        /// 但只铺**多册的非核心、非成长**牌（小精灵/归根/虚化这种重复件），成长留着手打、核心留手。</summary>
        public override bool WantLand(GameCore core, Player me, Card card)
        {
            if (TotalLands(core, me) >= GateLands) return false;
            string name = NameOf(card);
            if (name == null) return false;
            if (CoreNames.Contains(name) || name == NameVeil) return false;
            if (RampNames.Contains(name)) return false;
            return HandCopies(core, me, name) > 1;
        }

        /// <summary>铺地优先级：多册的先用（小精灵/萌芽这类重复牌当土地不心疼），
        /// 同册数下低费先用；核心组件垫底（正常不会进候选）。</summary>
        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
        {
            string name = NameOf(card);
            if (name != null && CoreNames.Contains(name)) return -1000f;
            int copies = name != null ? HandCopies(core, me, name) : 1;
            return copies * 100f - totalCost(card);
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
                var stuck = CoreStuckInPool(core, me);
                if (stuck.Count > 0)
                {
                    var rescue = list.Where(e => e is Card c && stuck.Contains(c)).ToList();
                    if (rescue.Count > 0) return rescue;
                }
                var ordered = list.OrderByDescending(e => e is Card c ? SacrificeScore(core, me, c) : 0f).ToList();
                int need = Math.Max(1, Math.Min(ShortfallNeed(core, me), ordered.Count));
                return ordered.Take(need).ToList();
            }

            // 回收（归根）：核心组件 > 小精灵 > 成长 > 其他
            if (atom == AtomicEffectType.RecoverToHand)
                return list.OrderByDescending(e => e is Card c ? RecoverScore(c) : 0f)
                           .ThenByDescending(e => e is Card c2 ? core.LayerEngine.CalculatePower(c2) : 0)
                           .ToList();

            // 抹除：对方单位按威胁（攻击力）优先；结界按类别；地牌随便（对方池里任意一张）
            if (atom == AtomicEffectType.Erase)
                return list.OrderByDescending(e => e is Card c
                        ? (ReferenceEquals(c.GetController(), me.Opponent)
                            ? 1000f + core.LayerEngine.CalculatePower(c)
                            : -1000f)
                        : 0f)
                    .ToList();

            // 护甲（小精灵登场）：角色优先（角色死了就输）
            if (atom == AtomicEffectType.AddArmor)
                return list.OrderByDescending(e => e is Player).ToList();

            // 减费奖励（2026-11-14 用户定案）：不给「最贵」，给**下回合要用的**——
            // 前期=成长（萌芽/沃土，快点铺地）、中期=两种大生物（古木守卫/暗影豹）、后期=回收（归根）。
            if (atom == AtomicEffectType.AddCostDownGreen)
            {
                int lands = TotalLands(core, me);
                return list.OrderByDescending(e => e is Card c ? CostDownPhaseScore(NameOf(c), lands) : 0f)
                    .ThenByDescending(e => e is Card c2 ? GameActions.GetCardCost(c2, 0).Total : 0f)
                    .ToList();
            }

            // 默认：对方威胁优先
            return list.OrderByDescending(e => e is Card c && c.IsAlive && ReferenceEquals(c.GetController(), me.Opponent)
                    ? core.LayerEngine.CalculatePower(c)
                    : 0f)
                .ToList();
        }

        /// <summary>牺牲落点估值：池中核心 3000 / 小精灵 800 / 普通地 500 / 场上核心 −3000（绝不能祭）。</summary>
        private static float SacrificeScore(GameCore core, Player me, Card c)
        {
            string name = NameOf(c);
            bool inPool = c.GetZone() == Zone.ElementPool;
            if (name != null && CoreNames.Contains(name)) return inPool ? 3000f : -3000f;
            if (inPool) return name == NameSpirit ? 800f : 500f;
            if (name == NameSpirit) return 300f;
            return 0f;
        }

        private static float RecoverScore(Card c)
        {
            string name = NameOf(c);
            if (name != null && CoreNames.Contains(name)) return 1000f;
            if (name == NameSpirit) return 600f;
            if (name != null && RampNames.Contains(name)) return 400f;
            return 100f;
        }

        /// <summary>减费落点（2026-11-14 用户定案）：按「下回合要用的」分阶段——前期成长、中期大生物、后期回收。</summary>
        private static float CostDownPhaseScore(string name, int lands)
        {
            if (name == null) return 0f;
            if (lands < GateLands)   // 前期：成长优先（萌芽 &gt; 沃土）
                return name == NameSprout ? 100f : name == NameBloom ? 90f : 10f;
            if (lands < 14)          // 中期：两张大生物
                return name == NameWall ? 100f : name == NamePanther ? 95f : 20f;
            return name == NameReturn ? 100f : 30f;   // 后期：回收
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

        /// <summary>缺口：手里「最接近可打」的那张牌还差几点费（0=没有可补的）。留手牌不算。</summary>
        private static int ShortfallNeed(GameCore core, Player me)
        {
            int avail = AvailableMana(core, me);
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

        /// <summary>可祭废料量：地牌池张数 + 场上 1/1 小精灵数（不把核心/大生物算作废料）。</summary>
        private static int FodderCount(GameCore core, Player me)
        {
            int n = core.ElementPool.GetPooledCards(me).Count();
            n += (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c != null && c.IsAlive && Is(c, NameSpirit));
            return n;
        }

        /// <summary>ShouldHold 的静态可用版（ShortfallNeed 里不能用实例方法递归调用）。</summary>
        private static bool ShouldHoldStatic(GameCore core, Player me, Card card)
        {
            string name = NameOf(card);
            if (name == null) return false;
            if (EraseNames.Contains(name)) return !MatchesOpponentMost(name, core, me);
            if (name == NamePanther) return TotalLands(core, me) < GateLands;
            if (name == NameReturn) return CoreInGraveyard(core, me) == 0 && GyCount(core, me) < 3;
            return false;
        }
        // ======================================== 局面读数 ========================================

        /// <summary>对手场上三种「卡型」数量：生物 / 结界（非生物战场卡）/ 元素池地牌。</summary>
        private static (int creatures, int enchantments, int lands) OpponentTypeCounts(GameCore core, Player me)
        {
            var opp = me.Opponent;
            int creatures = 0, enchants = 0;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
            {
                if (c == null || !c.IsAlive) continue;
                var d = (c as CardWrapper)?.GetData();
                if (d != null && d.Supertype == Cardtype.Creature) creatures++;
                else enchants++;
            }
            int lands = core.ElementPool.GetPooledCards(opp).Count();
            return (creatures, enchants, lands);
        }

        /// <summary>这张抹除是否命中对手最多的那一类（并列时按 生物&gt;结界&gt;地 优先）。</summary>
        private static bool MatchesOpponentMost(string eraseName, GameCore core, Player me)
        {
            var (creatures, enchants, lands) = OpponentTypeCounts(core, me);
            if (creatures + enchants + lands == 0) return false;
            if (creatures >= enchants && creatures >= lands) return eraseName == NameEraseCreature;
            if (enchants >= lands) return eraseName == NameEraseEnchant;
            return eraseName == NameEraseLand;
        }

        /// <summary>地牌池里躺着的核心组件（被成长随机拉进去的）。</summary>
        private static List<Card> CoreStuckInPool(GameCore core, Player me)
        {
            var result = new List<Card>();
            foreach (var pc in core.ElementPool.GetPooledCards(me))
            {
                if (pc?.SourceCard == null) continue;
                string name = NameOf(pc.SourceCard);
                if (name != null && CoreNames.Contains(name)) result.Add(pc.SourceCard);
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

        private static int EnemyBodies(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c != null && c.IsAlive);

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
            => core.ElementPool.GetPooledCards(me).Count(pc => pc != null && pc.LockedColor == color);

        private static int TotalLands(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me).Count();
    }
}
