using System.Linq;

namespace CardCore
{
    /// <summary>
    /// OutcomeGate 运行时条件评估器。
    /// 仅服务「分支步骤」（伤害/治疗/信息族）：读取当前目标的 EffectOutcome 判断门槛是否达成。
    /// Drawback（抽牌减费缺陷）与 FilterPrecision（检索按维度计费）不走此路径。
    /// 条件 id 取自 Configs/BranchConfig.json（见 BranchConfigTable）。
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

                // ---- 信息族 ----
                case "TypeProphecyHit":
                    return EvaluateProphecy(stringParam, context);

                default:
                    return false;
            }
        }

        /// <summary>
        /// 类型预言：查看对手手牌前指定一个类型（Cardtype 名），对手手牌含该类型则命中。
        /// </summary>
        private static bool EvaluateProphecy(string typeName, EffectExecutionContext context)
        {
            if (string.IsNullOrEmpty(typeName) || context == null || context.ZoneManager == null)
                return false;

            var controller = context.Controller;
            var opponent = controller?.Opponent;
            if (opponent == null) return false;

            if (!System.Enum.TryParse<Cardtype>(typeName, true, out var prophecyType))
                return false;

            var hand = context.ZoneManager.GetCards(opponent, Zone.Hand);
            if (hand == null) return false;

            return hand.Any(c => c is IHasSupertype st && st.Supertype == prophecyType);
        }
    }
}
