using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 分支条件运行时评估器。两族：
    /// ① 产出条件族（自由分支·Outcome，2026-10-05 定案收敛五项）：DmgKillsTarget/DeclareHit/DeclareMiss
    ///   读当前目标的 EffectOutcome 即时判定；ProphecyHit/Miss 为延迟验证——引擎拦截为 PendingProphecy，
    ///   由 ProphecySystem 在验证时刻结算（此处兜底恒假）。
    /// ② 局面状态族（2026-09-22）读 EffectExecutionContext（生命/卡组/生物对比、准备阶段抽牌、首张出牌）——
    ///   通用门，任意主效果原子可挂，per-target 结果恒同；
    /// ③ 诅咒门（2026-10-05）施放时恒假——分支只是载荷声明，抽到时由 CurseSystem 结算。
    /// 产出族其余条件（TargetSurvived/Overkill/TargetStillWounded/Overheal）已删（2026-10-05 用户定案）。
    /// 拦截式改写族（DmgRewrite*）已随四条改写迁往唯一光环退役（2026-10-05）。
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
                case "DmgKillsTarget":
                    return outcome.AnyKilled;

                // ---- 信息族：宣言（即时验证，读产出） ----
                case "DeclareHit":
                    return outcome.DeclareHit;
                case "DeclareMiss":
                    return !outcome.DeclareHit;

                // ---- 信息族：预言（延迟验证）----
                // 不应被内联评估：执行引擎拦截为 PendingProphecy，由 ProphecySystem 在验证时刻结算。
                // 此处为防御性兜底（未命中）。
                case "ProphecyHit":
                case "ProphecyMiss":
                    return false;

                // ---- 局面状态族（2026-09-22 定案：读 context 而非产出，通用门——任意主效果原子可挂）----
                // per-target 评估结果恒同（幂等读局面），沿用逐目标评估无害。
                case "DrawnInStandbyThisTurn": // 本回合第一张抽到的卡（宿主卡身份，效果抽牌不算）
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

                // ---- 诅咒门（2026-10-05 诅咒有限分支定案）：施放时恒假不结算——
                // 分支只是载荷声明（converter 把 Then 原子抽取挂 AddCurse 主干原子的 CursePayload），
                // 真正的触发点=对手抽到该卡（CurseSystem.OnCardDrawn 驱动）。----
                case "CurseOnDraw":
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
                case "DmgKillsTarget":
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
                // 诅咒门（2026-10-05，施放时恒假——载荷声明，抽到时由 CurseSystem 结算）
                case "CurseOnDraw":
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
