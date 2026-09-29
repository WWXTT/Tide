using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 效果文本单一真相（2026-09-16 描述接口化定案）：实体命名 + 执行产出句式。
    /// 供 AtomicEffectHandlerBase.GetDescription 的上下文合成与 MatchLogRenderer 战报行共用——
    /// 描述句式不再两套各自为政。
    /// </summary>
    public static class EffectText
    {
        /// <summary>实体短名（玩家名 / 卡名 / 卡 ID）</summary>
        public static string Name(Entity entity) => entity == null ? "∅"
            : entity is Player p ? p.Name
            : entity is IHasName n && !string.IsNullOrEmpty(n.CardName) ? n.CardName
            : entity is Card c ? c.ID
            : entity.ToString();

        /// <summary>产出是否可读（有实际伤害/治疗/击杀/受影响目标/宣言）</summary>
        public static bool HasReadableOutcome(EffectOutcome o)
            => o != null && (o.DamageDealt > 0 || o.HealApplied > 0
                          || o.KilledTargets.Count > 0 || o.AffectedTargets.Count > 0
                          || o.Declaration != null);

        /// <summary>同名聚合的紧凑卡牌清单（"火球、嘲讽守卫×2、…"）</summary>
        public static string DescribeCards(List<Card> cards)
            => cards == null || cards.Count == 0 ? "∅"
               : string.Join("、", cards.GroupBy(Name)
                                        .Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key));

        public static string DescribeEntities(List<Entity> list)
            => list == null || list.Count == 0 ? "∅" : string.Join("、", list.Select(Name));

        /// <summary>
        /// 执行产出描述（真实结算值——LastOutcome 由 handler 在结算时写入，描述不重掷随机）。
        /// 无可读产出返回空串。
        /// </summary>
        public static string DescribeOutcome(EffectOutcome o)
        {
            if (!HasReadableOutcome(o)) return "";
            if (o.DamageDealt > 0)
                return $"{DescribeEntities(o.AffectedTargets)} 共受 {o.DamageDealt} 伤害"
                       + (o.KilledTargets.Count > 0 ? $"，{DescribeEntities(o.KilledTargets)} 死亡" : "");
            if (o.HealApplied > 0)
                return $"{DescribeEntities(o.AffectedTargets)} 回复 {o.HealApplied} 生命"
                       + (o.OverhealAmount > 0 ? $"（溢出 {o.OverhealAmount}）" : "");
            if (o.Declaration != null)
                return $"宣言「{o.Declaration}」{(o.DeclareHit ? "命中" : "未命中")}";
            return $"作用于 {DescribeEntities(o.AffectedTargets)}";
        }
    }
}
