using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 场地光环投放原子（规则轴路线 B，2026-10-03 定案；ModifyGameRule 转正；2026-10-07 作用范围化；
    /// 2026-10-31 阶段D EffectType 唯一化）：**规则 id 改从行类型派生**——每条光环一型
    ///（AuraTriPhase..AuraInscription，29 型），类型→规则短名静态映射=
    /// RuleAuraComponents.RuleIdOfType（源=RuleAuraComponents 常量），str=规则短名口径彻底退役
    ///（表 EffectType 列未翻新期间行仍=ModifyGameRule，按 str 回落过渡；旧卡带 str 新链路忽略）。
    /// Value=作用范围序（RuleAuraScope：0=己方/1=双方（缺省）/2=对方——合成器按表行 Polarity 门控可选项；
    /// 2026-10-29 起固定双方规则忽略 value 恒 Both，计价去对称让利=表价即实价）。挂在结界卡登场效果上——载体（CastCard）入场结算时
    /// 激活 RuleAuraSystem：持续永久、全局唯一（新换旧送墓）、载体离场失效、
    /// 无方向箭头（非连接光环）。效果/指示物分离定案延续：本原子只负责投放，
    /// 规则体由 RuleAuraSystem/RuleHooks/ReplacementEngine 承载。
    /// </summary>
    public class ModifyGameRuleHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyGameRule;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var ruleId = RuleIdOf(effect);
            if (string.IsNullOrEmpty(ruleId))
            {
                TideLog.Warn("[ModifyGameRuleHandler] 规则 id 缺失（行类型不在光环映射且 str 空），空转");
                return;
            }

            var carrier = context.CastCard ?? context.Source as Card;
            if (carrier == null)
            {
                TideLog.Warn("[ModifyGameRuleHandler] 无载体（CastCard/Source 非卡），空转");
                return;
            }

            // 固定双方规则（2026-10-29 1b 定案）：许可/禁止族 UI 不提供单侧选择——忽略实例 value 恒 Both；
            // 改写族照走原子步 value（RuleAuraScope 序）。
            int scope = RuleAuraComponents.IsFixedBothRule(ruleId) ? (int)RuleAuraScope.Both : effect.Value;
            RuleAuraSystem.Activate(ruleId, carrier, context.Controller, scope);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            var ruleId = RuleIdOf(effect);
            return $"场地光环：{ruleId}（{RuleAuraSystem.RuleAuraScopeZh(effect.Value)}，载体离场失效）";
        }

        /// <summary>规则 id 解析：行新型经 RuleAuraComponents.RuleIdOfType（单源映射）；
        /// 旧共享型 ModifyGameRule（表 EffectType 列未翻新行/旧实例数据）回落 str=规则短名。</summary>
        internal static string RuleIdOf(AtomicEffectInstance effect)
            => RuleAuraComponents.RuleIdOfType(effect.Type)
               ?? (effect.Type == AtomicEffectType.ModifyGameRule ? effect.StringValue : null);
    }
}
