using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 功能内容哈希 —— 用于效果库与卡表去重。
    ///
    /// 只哈希「功能内容」（决定效果/卡牌行为的字段），刻意排除名称、标签、描述等
    /// 非区分性展示信息：名字不同但功能相同的两条目，视为重复（覆盖）。
    ///
    /// 复用 CardCore.MurmurHash3.Hash32，产物为稳定 8 位大写 hex 串。
    /// </summary>
    public static class ContentHasher
    {
        /// <summary>效果图功能哈希（排除 header 的 DisplayName/Description/Tags）。</summary>
        public static string HashEffect(EffectGraphData graph)
        {
            var sb = new StringBuilder();
            AppendEffect(sb, graph);
            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        /// <summary>卡牌功能哈希（排除 CardName/Tags；Keywords 影响功能，纳入）。</summary>
        public static string HashCard(CardData card)
        {
            var sb = new StringBuilder();
            sb.Append("ST:").Append((int)card.Supertype).Append('|');
            sb.Append("SUB:").Append((int)card.Subtype).Append('|');
            sb.Append("P:").Append(card.Power ?? 0).Append('|');
            sb.Append("L:").Append(card.Life ?? 0).Append('|');
            sb.Append("LV:").Append(card.Level ?? -1).Append('|');
            sb.Append("RK:").Append(card.Rank ?? -1).Append('|');
            sb.Append("LR:").Append(card.LinkRating ?? -1).Append('|');
            sb.Append("AR:").Append((int)card.ArrowDirections).Append('|');

            sb.Append("COST:");
            if (card.Cost != null)
            {
                foreach (var kv in card.Cost.OrderBy(k => k.Key))
                {
                    sb.Append(kv.Key).Append('=').Append(kv.Value).Append(',');
                }
            }
            sb.Append('|');

            sb.Append("KW:");
            foreach (var kw in (card.Keywords ?? new List<string>()).OrderBy(k => k))
            {
                sb.Append(kw).Append(',');
            }
            sb.Append('|');

            sb.Append("FX:");
            if (card.Effects != null)
            {
                foreach (var fx in card.Effects)
                {
                    AppendCardEffect(sb, fx);
                    sb.Append(';');
                }
            }

            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        private static void AppendEffect(StringBuilder sb, EffectGraphData graph)
        {
            if (graph == null)
            {
                return;
            }
            // header 仅取影响功能的字段（时点/激活/代价），忽略名称/描述/标签。
            var h = graph.header;
            if (h != null)
            {
                sb.Append("TT:").Append(h.TriggerTiming).Append('|');
                sb.Append("AT:").Append(h.ActivationType).Append('|');
                AppendCosts(sb, h.Costs);
            }
            sb.Append("STEPS:");
            if (graph.steps != null)
            {
                foreach (var step in graph.steps)
                {
                    AppendStep(sb, step);
                    sb.Append(';');
                }
            }
        }

        private static void AppendCardEffect(StringBuilder sb, CardEffectData fx)
        {
            if (fx == null)
            {
                return;
            }
            sb.Append("TT:").Append(fx.TriggerTiming).Append(',');
            sb.Append("AT:").Append(fx.ActivationType).Append(',');
            AppendCosts(sb, fx.Costs);
            if (fx.Steps != null && fx.Steps.Count > 0)
            {
                foreach (var step in fx.Steps)
                {
                    AppendStep(sb, step);
                }
            }
            else if (fx.AtomicEffects != null)
            {
                foreach (var atom in fx.AtomicEffects)
                {
                    AppendAtomic(sb, atom);
                }
            }
        }

        private static void AppendStep(StringBuilder sb, EffectStepData step)
        {
            if (step == null)
            {
                return;
            }
            sb.Append('k').Append(step.kind).Append(':');
            if (step.kind == 0)
            {
                AppendAtomic(sb, step.atomic);
            }
            else
            {
                AppendCondition(sb, step.condition);
                sb.Append("then{");
                if (step.thenSteps != null)
                {
                    foreach (var a in step.thenSteps) AppendAtomic(sb, a);
                }
                sb.Append("}else{");
                if (step.elseSteps != null)
                {
                    foreach (var a in step.elseSteps) AppendAtomic(sb, a);
                }
                sb.Append('}');
            }
        }

        private static void AppendAtomic(StringBuilder sb, AtomicEffectEntry a)
        {
            if (a == null)
            {
                return;
            }
            sb.Append('[')
              .Append(a.EffectType).Append(',')
              .Append(a.Value).Append(',')
              .Append(a.Value2).Append(',')
              .Append(a.StringValue).Append(',')
              .Append(a.ManaTypeParam).Append(',')
              .Append(a.ZoneParam).Append(',')
              .Append(a.Duration).Append(',')
              .Append(a.TargetTypeOverride).Append(',')
              .Append(a.TargetFilterOverride).Append(',')
              .Append(a.TargetCountOverride).Append(',')
              .Append(a.TargetScopeOverride)
              .Append(']');
        }

        private static void AppendCondition(StringBuilder sb, ActivationConditionData c)
        {
            if (c == null)
            {
                return;
            }
            sb.Append('(')
              .Append(c.Type).Append(',')
              .Append(c.Value).Append(',')
              .Append(c.Value2).Append(',')
              .Append(c.StringValue).Append(',')
              .Append(c.Negate ? 1 : 0)
              .Append(')');
        }

        private static void AppendCosts(StringBuilder sb, List<CostEntry> costs)
        {
            sb.Append("CO:");
            if (costs != null)
            {
                foreach (var c in costs)
                {
                    if (c == null) continue;
                    sb.Append('<')
                      .Append(c.CostType).Append(',')
                      .Append(c.Value).Append(',')
                      .Append(c.ManaType).Append(',')
                      .Append(c.TurnDuration)
                      .Append('>');
                }
            }
            sb.Append('|');
        }
    }
}
