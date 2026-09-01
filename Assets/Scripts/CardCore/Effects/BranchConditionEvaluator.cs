using System.Linq;

namespace CardCore
{
    /// <summary>
    /// OutcomeGate 运行时条件评估器。
    /// 仅服务「分支步骤」：宣言族读当前目标的 EffectOutcome 即时判定；
    /// 伤害/治疗族读产出记录。Drawback（抽牌减费缺陷）与 FilterPrecision（检索按维度计费）
    /// 不走此路径。条件 id 取自 Configs/BranchConfig.json（见 BranchConfigTable），
    /// 目录与这里的 switch 是唯一同步点——加载自检会核对两侧一致。
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

                default:
                    return false;
            }
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
