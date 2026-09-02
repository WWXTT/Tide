using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 护甲原子：为目标添加 N 点护甲指示物（counter：KeywordRules.ArmorCounter）。
    /// 伤害结算时先逐点吸收护甲，再走坚韧减免、最后扣生命。
    /// 这是参数化原子（{value}），与固定 −1 的坚韧关键词互补（用户定案）。
    /// </summary>
    public class AddArmorHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddArmor;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            if (amount <= 0) return;

            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                card.AddCounters(KeywordRules.ArmorCounter, amount);
                PublishEvent(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = KeywordRules.ArmorCounter,
                    Amount = amount,
                    Source = context.Source,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}点护甲";
    }
}
