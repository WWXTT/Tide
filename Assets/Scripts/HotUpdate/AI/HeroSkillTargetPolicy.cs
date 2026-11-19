using System.Collections.Generic;
using CardCore;

namespace CardCore.AI
{
    /// <summary>
    /// 英雄技能无域原子的候选顺序/排除单源（P0 2026-11-19，docs/AI动作空间补全_修改计划.md §1.2.4）：
    /// 牺牲（Sacrifice）/蛰伏（Dormancy）的作用域写死在 handler、原子表无 Domain——
    /// LAE prefix-k 行（LegalActionEnumerator.AddPrefixRows）与 GreenRampStrategy.HeroSkillTargets
    /// **共用本类**决定「哪些候选可进名单、按什么顺序」，不得各写一份。
    /// 顺序语义：场上段在前、池段在后，组内保持传入（引擎解析）序——与 GreenRampStrategy
    /// HeroSkillTargets 2026-11-18 定案的顺序逐行等价（A/B 200 局验证口径）。
    /// 卡名常量依赖（归根/阿格洛斯）：卡改名时须同批改常量并全仓 grep（AGENTS.md 数值/常量纪律）。
    /// </summary>
    public static class HeroSkillTargetPolicy
    {
        /// <summary>归根（牺牲名单排除——回收引擎核心，无论在场与否不献）。</summary>
        private const string NameReturn = "归根";

        /// <summary>系命者 阿格洛斯（**场上的**不献=站场单位；池里的照献，献进墓地由归根回收当回合再打出）。</summary>
        private const string NamePanther = "系命者 阿格洛斯";

        /// <summary>牺牲候选排序（2026-11-18 定案口径）：
        /// 场上段=己方战场存活单位/结界，排除「归根」与**场上的**「阿格洛斯」；
        /// 池段=其余候选全部保留（含池里的阿格洛斯/归根副本）；组内保持传入序。
        /// GreenRampStrategy.HeroSkillTargets 传「场上全部+池两路」原始候选，本口过滤+排序；
        /// LAE 传白名单域轴解析候选（NoDomainTargetScopes 牺牲域），同口。</summary>
        public static List<Entity> OrderSacrifice(GameCore core, Player me, List<Entity> candidates)
        {
            var result = new List<Entity>();
            var poolTail = new List<Entity>();
            if (candidates == null) return result;
            var seen = new HashSet<Entity>();
            foreach (var e in candidates)
            {
                if (e == null || !seen.Add(e)) continue;
                if (!(e is Card c)) continue;
                if (c.GetZone() == Zone.Battlefield)
                {
                    // 场上段：非归根/非阿格洛斯 且存活（标死未送墓的不献——handler 同判跳过）
                    string name = (c as IHasName)?.CardName;
                    if (!c.IsAlive || name == NameReturn || name == NamePanther) continue;
                    result.Add(e);
                }
                else
                {
                    poolTail.Add(e); // 池段全留（含池里的阿格洛斯/归根副本）
                }
            }
            result.AddRange(poolTail);
            return result;
        }

        /// <summary>蛰伏候选过滤（DormancyHandler 作用域同口径）：只留「己方元素池**未横置**的地」，保持传入序。
        /// 未横置态查 PooledCard 包装——池内地牌的横置态不在 Card._isTapped 上（支付/蛰伏横置
        /// 都写包装位），TargetFilter 的 Untapped token 对池地无效，必须经池包装判定。</summary>
        public static List<Entity> OrderDormancy(GameCore core, Player me, List<Entity> candidates)
        {
            var result = new List<Entity>();
            if (candidates == null || core?.ElementPool == null || me == null) return result;
            var untapped = new HashSet<Card>();
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc?.SourceCard != null && !pc.IsTapped) untapped.Add(pc.SourceCard);
            foreach (var e in candidates)
                if (e is Card c && untapped.Contains(c)) result.Add(e);
            return result;
        }
    }
}
