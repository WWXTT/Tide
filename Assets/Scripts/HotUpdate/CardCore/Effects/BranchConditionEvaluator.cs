using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// OutcomeGate 运行时条件评估器。
    /// 仅服务「分支步骤」，三族：
    /// ① 产出族（宣言/伤害/治疗）读当前目标的 EffectOutcome 即时判定；
    /// ② 局面状态族（2026-09-22）读 EffectExecutionContext（生命/卡组/生物对比、准备阶段抽牌、首张出牌）——
    ///   通用门，任意主效果原子可挂，per-target 结果恒同；
    /// ③ 改写族（2026-09-22 拦截式）不走内联评估——执行引擎在主干原子执行前拦截（IsRewriteCondition）。
    /// FilterPrecision（检索按维度计费）不走此路径（抽牌减费缺陷 Drawback 已随减费归入代价体系退役 2026-09-16）。
    /// </summary>
    public static class BranchConditionEvaluator
    {
        public static bool Evaluate(
            string conditionId,
            EffectOutcome outcome,
            int param = 0,
            string stringParam = null,
            EffectExecutionContext context = null)
        {
            if (string.IsNullOrEmpty(conditionId) || outcome == null)
                return false;

            switch (conditionId)
            {
                // ---- 伤害族 ----
                case "TargetSurvived":
                    return outcome.AnySurvived;
                case "DmgKillsTarget":
                    return outcome.AnyKilled;
                case "Overkill":
                    return outcome.DamageDealt - outcome.TargetLifeBefore > 0;

                // ---- 治疗族 ----
                case "TargetStillWounded":
                    return outcome.AffectedTargets.Any(t => t != null && t.GetLife() < t.GetMaxLife());
                case "Overheal":
                    return outcome.OverhealAmount > 0;

                // ---- 信息族：宣言（即时验证，读产出） ----
                case "DeclareHit":
                    return outcome.DeclareHit;
                case "DeclareMiss":
                    return !outcome.DeclareHit;

                // ---- 信息族：预言（延迟验证）----
                // 不应被内联评估：执行引擎在 ApplyGateRewardsAsync 拦截为 PendingProphecy，
                // 由 ProphecySystem 在验证时刻结算。此处为防御性兜底（未命中）。
                case "ProphecyHit":
                case "ProphecyMiss":
                    return false;

                // ---- 局面状态族（2026-09-22 定案：读 context 而非产出，通用门——任意主效果原子可挂）----
                // per-target 评估结果恒同（幂等读局面），沿用逐目标评估无害。
                case "DrawnInStandbyThisTurn": // 本回合准备阶段抽到的卡（宿主卡身份，效果抽牌不算）
                    return HostCard(context) != null
                        && MatchStatsService.Instance?.WasDrawnInStandbyThisTurn(context.Controller, HostCard(context)) == true;
                case "LifeBelowOpp":   // 生命低于对手
                    return Opp(context) != null && context.Controller.GetLife() < Opp(context).GetLife();
                case "LifeAboveOpp":   // 生命高于对手
                    return Opp(context) != null && context.Controller.GetLife() > Opp(context).GetLife();
                case "DeckBelowOpp":   // 卡组剩余低于对手
                    return Opp(context) != null && DeckCount(context, context.Controller) < DeckCount(context, Opp(context));
                case "DeckAboveOpp":   // 卡组剩余高于对手
                    return Opp(context) != null && DeckCount(context, context.Controller) > DeckCount(context, Opp(context));
                case "CreaturesBelowOpp": // 场上生物低于对手
                    return Opp(context) != null && CreatureCount(context, context.Controller) < CreatureCount(context, Opp(context));
                case "CreaturesAboveOpp": // 场上生物高于对手
                    return Opp(context) != null && CreatureCount(context, context.Controller) > CreatureCount(context, Opp(context));
                case "FirstCardThisTurn": // 本回合使用的第一张卡（CardPlayEvent 宣言点已计数，含正在结算的这张）
                    return MatchStatsService.Instance != null
                        && MatchStatsService.Instance.GetStat(context.Controller,
                            MatchStatsService.CardsPlayed, StatScope.ThisTurn) == 1;
                // ---- 局面状态族·二批（2026-09-22 追加，预算 2）----
                case "LandsGe7":   // 操控地数量 ≥ 7（地牌=元素池中未耗尽的地牌张数）
                    return context?.Controller != null && context.ElementPool != null
                        && context.ElementPool.GetPooledCards(context.Controller).Count >= 7;
                case "HandEmpty":  // 手牌数量 = 0
                    return context?.Controller != null
                        && (context.ZoneManager?.GetCards(context.Controller, Zone.Hand)?.Count ?? 1) == 0;
                case "LifeLe7":    // 角色生命值 ≤ 7
                    return context?.Controller != null && context.Controller.GetLife() <= 7;

                // ---- 改写族（2026-09-22 拦截式改写门）----
                // 不走内联评估：执行引擎在主干原子执行**前**拦截（伤害不发生，改为施加指示物）——
                // 见 EffectExecutionEngine.ExecuteStepSequenceAsync 与 IsRewriteCondition。
                case "DmgRewriteToxin":
                case "DmgRewriteFreeze":
                case "DmgRewriteSleep":
                case "DmgRewriteVenom":
                    return false;

                default:
                    return false;
            }
        }

        private static Player Opp(EffectExecutionContext context) => context?.Controller?.Opponent;

        /// <summary>宿主卡：施放链路上的卡实例（魔法卡 Source=角色，先读 CastCard 退回 Source）。</summary>
        private static Card HostCard(EffectExecutionContext context)
            => context == null ? null : (context.CastCard ?? context.Source as Card);

        private static int DeckCount(EffectExecutionContext context, Player player)
            => context?.ZoneManager?.GetCards(player, Zone.Deck)?.Count ?? 0;

        private static int CreatureCount(EffectExecutionContext context, Player player)
        {
            var cards = context?.ZoneManager?.GetCards(player, Zone.Battlefield);
            if (cards == null) return 0;
            return cards.Count(c => c is IHasSupertype st && st.Supertype == Cardtype.Creature);
        }

        /// <summary>条件 id 是否被评估器识别（BranchConfigTable 加载自检用，保证目录与代码同步）</summary>
        public static bool IsKnownCondition(string conditionId)
        {
            switch (conditionId)
            {
                case "TargetSurvived":
                case "DmgKillsTarget":
                case "Overkill":
                case "TargetStillWounded":
                case "Overheal":
                case "DeclareHit":
                case "DeclareMiss":
                case "ProphecyHit":
                case "ProphecyMiss":
                // 局面状态族（2026-09-22）
                case "DrawnInStandbyThisTurn":
                case "LifeBelowOpp":
                case "LifeAboveOpp":
                case "DeckBelowOpp":
                case "DeckAboveOpp":
                case "CreaturesBelowOpp":
                case "CreaturesAboveOpp":
                case "FirstCardThisTurn":
                // 局面状态族·二批（2026-09-22 追加）
                case "LandsGe7":
                case "HandEmpty":
                case "LifeLe7":
                // 改写族（2026-09-22，拦截式——不走内联评估）
                case "DmgRewriteToxin":
                case "DmgRewriteFreeze":
                case "DmgRewriteSleep":
                case "DmgRewriteVenom":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>是否为拦截式改写门（2026-09-22 定案）：执行引擎据此在主干原子执行**前**拦截——
        /// 跳过伤害、对目标施加对应指示物（固定 1 层，同改写族关键词口径）；
        /// 无 then/else 奖励槽（改写本身就是分支效果）。挂载限定伤害族主干（converter 守卫）。</summary>
        public static bool IsRewriteCondition(string conditionId)
        {
            switch (conditionId)
            {
                case "DmgRewriteToxin":
                case "DmgRewriteFreeze":
                case "DmgRewriteSleep":
                case "DmgRewriteVenom":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>是否为延迟验证条件（预言族）——执行引擎据此把分支打包成 PendingProphecy 而非即时结算</summary>
        public static bool IsDelayedCondition(string conditionId)
        {
            return conditionId == "ProphecyHit" || conditionId == "ProphecyMiss";
        }
    }
}
