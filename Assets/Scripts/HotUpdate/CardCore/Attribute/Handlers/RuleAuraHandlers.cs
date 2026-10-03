using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 规则光环投放原子（规则轴路线 B，2026-10-03 定案；ModifyGameRule 转正）：
    /// str=规则短名（RuleAuraComponents 的七常量——与原子表 7 行一一对应），
    /// Value 无用（规则参数由各规则自持）。挂在结界卡登场效果上——载体（CastCard）入场结算时
    /// 激活 RuleAuraSystem：对双方生效、持续永久、全局唯一（新换旧送墓）、载体离场失效、
    /// 无方向箭头（非连接光环）。效果/指示物分离定案延续：本原子只负责投放，
    /// 规则体由 RuleAuraSystem/RuleHooks/ReplacementEngine 承载。
    /// </summary>
    public class ModifyGameRuleHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyGameRule;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var ruleId = effect.StringValue;
            if (string.IsNullOrEmpty(ruleId))
            {
                TideLog.Warn("[ModifyGameRuleHandler] 缺少规则短名（str 应为规则光环 id），空转");
                return;
            }

            var carrier = context.CastCard ?? context.Source as Card;
            if (carrier == null)
            {
                TideLog.Warn("[ModifyGameRuleHandler] 无载体（CastCard/Source 非卡），空转");
                return;
            }

            RuleAuraSystem.Activate(ruleId, carrier, context.Controller);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"规则光环：{effect.StringValue}（对双方生效，载体离场失效）";
    }
}
