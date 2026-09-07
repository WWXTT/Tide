using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 护甲原子：为目标添加 N 点护甲指示物（counter：KeywordRules.ArmorCounter，Entity 级——角色可持有）。
    /// 伤害结算时先逐点吸收护甲，再走坚韧减免、最后扣生命。
    /// 这是参数化原子（{value}），与固定 −1 的坚韧关键词互补（用户定案）。
    /// 指示物无持续时间 → 换区清除（CounterRules.ClearAll 统一口径）。
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
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(KeywordRules.ArmorCounter, amount, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.ArmorCounter,
                    Amount = amount,
                    Source = context.Source,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}点护甲";
    }

    /// <summary>
    /// 毒素原子（新增）：对目标附加毒素指示物——持续 3 回合，回合结束时持有者每层受 1 点伤害，可叠加。
    /// 层带独立回合时钟（CounterRules.ToxinCounter，ForTurns=3，CounterRules.OnTurnEnd 统一计时）。
    /// </summary>
    public class AddToxinHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddToxin;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                var spec = CounterRules.Find(CounterRules.ToxinCounter);
                target.AddCounters(CounterRules.ToxinCounter, stacks, spec.Turns, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.ToxinCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "附加毒素指示物（回合结束1伤，持续3回合，可叠加）";
    }

    /// <summary>
    /// 紊乱指示物（补入表——突袭生效的残留物此前只有运行时无原子）：持续到回合结束，
    /// 期间持有者不能以玩家为目标（攻击与效果发动同口径，TargetFilterSystem/CombatSystem 强制）。
    /// </summary>
    public class RushSicknessHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RushSickness;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(KeywordRules.RushSicknessCounter, 1, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.RushSicknessCounter,
                    Amount = 1,
                    Source = context.Source,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "附加紊乱指示物（持续到回合结束，期间不能以玩家为目标）";
    }

    /// <summary>
    /// 易损指示物：持续 1 回合（每个回合末到期），受到伤害时每层使受到的伤害 +1
    /// （KeywordRules.ApplyDamage 第 0 步放大——圣盾/护甲吸收放大后的量）。
    /// </summary>
    public class AddVulnerableHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddVulnerable;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.VulnerableCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.VulnerableCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "附加易损指示物（持续1回合，受到伤害时每层+1）";
    }

    /// <summary>
    /// 单向属性指示物原子基类（攻/血×增减、±1/+1 六类）：{value}=层数（≤0 取 1），
    /// 每层效果固定 ±1（CounterRules.StatKind），加时回写、清除反写。
    /// </summary>
    public abstract class StatCounterAtomHandlerBase : AtomicEffectHandlerBase
    {
        /// <summary>指示物 id（须在 CounterRules 注册 StatKind）</summary>
        protected abstract string CounterId { get; }

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                CounterRules.AddStatCounter(card, CounterId, stacks, context.Source);
            }
        }
    }

    /// <summary>攻击力增加指示物（每层 +1 攻）</summary>
    public class AddPowerUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPowerUp;
        protected override string CounterId => CounterRules.PowerUpCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个攻击力增加指示物";
    }

    /// <summary>攻击力减少指示物（每层 −1 攻）</summary>
    public class AddPowerDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPowerDown;
        protected override string CounterId => CounterRules.PowerDownCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个攻击力减少指示物";
    }

    /// <summary>生命值增加指示物（每层 +1 上限与当前）</summary>
    public class AddLifeUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddLifeUp;
        protected override string CounterId => CounterRules.LifeUpCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个生命值增加指示物";
    }

    /// <summary>生命值减少指示物（每层 −1 上限，归零标死交 SBA）</summary>
    public class AddLifeDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddLifeDown;
        protected override string CounterId => CounterRules.LifeDownCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个生命值减少指示物";
    }

    /// <summary>属性增加指示物（+1/+1，成长同款）</summary>
    public class AddPlusOneHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPlusOne;
        protected override string CounterId => CounterRules.PlusOneCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个属性增加指示物（+1/+1）";
    }

    /// <summary>属性减少指示物（-1/-1）</summary>
    public class AddMinusOneHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddMinusOne;
        protected override string CounterId => CounterRules.MinusOneCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个属性减少指示物（-1/-1）";
    }

    /// <summary>虚弱：对{target}施加 {value} 个属性减少（= -1/-1 层 ×{value}）</summary>
    public class WeakenHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Weaken;
        protected override string CounterId => CounterRules.MinusOneCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"施加{effect.Value}个属性减少（-1/-1）";
    }

    /// <summary>鼓舞：对{target}施加 {value} 个属性增加（= +1/+1 层 ×{value}）</summary>
    public class InspireHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Inspire;
        protected override string CounterId => CounterRules.PlusOneCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"施加{effect.Value}个属性增加（+1/+1）";
    }

    /// <summary>费用增加指示物（每层 +1 费，仅手牌生效，离手消失）</summary>
    public class AddCostUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostUp;
        protected override string CounterId => CounterRules.CostUpCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个费用增加指示物";
    }

    /// <summary>费用减少指示物（每层 −1 费，仅手牌生效，离手消失）</summary>
    public class AddCostDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostDown;
        protected override string CounterId => CounterRules.CostDownCounter;
        public override string GetDescription(AtomicEffectInstance effect) => $"添加{effect.Value}个费用减少指示物";
    }
}
