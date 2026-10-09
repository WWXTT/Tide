using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 护甲原子（生效自减类）：为目标添加 {value} 层护甲指示物（KeywordRules.ArmorCounter，Entity 级——
    /// 角色可持有）。伤害结算时先逐点吸收护甲（每层 1 点，吸收即消耗），再走坚韧指示物减免、最后扣生命。
    /// 层改造定案（2026-10-08）：未开放 value 的行统一赋 1 层；计价=基础费用×层数（线性）。
    /// </summary>
    public class AddArmorHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddArmor;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            if (amount <= 0) amount = 1; // 未声明层数=统一 1 层（2026-10-08 定案，与计价 max(1,|value|) 同口径）

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

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"添加{(effect.Value > 0 ? effect.Value : 1)}层护甲指示物";
    }

    /// <summary>
    /// 坚韧原子（2026-10-08 指示物化定案，表行 2b1e3700——原子类型 GrantToughness，原 GrantArmor 同日更名；
    /// refId 迁移 05485f65→a312b8b0→758a74e0（同日哈希对齐）→2b1e3700，2026-10-09 生效自减改版随文案换号）：
    /// 对目标附加 {value} 层坚韧指示物（CounterRules.ToughnessCounter）——每次受到的伤害每层 −1，
    /// 实际拦到即生效、生效后层数减半（floor）、可叠加；Entity 级（角色可持有）；换区不清、净化可清。
    /// 取代旧 GrantKeywordHandler 关键词路径（台账 Value/Limit+次数闸+光环份额随之退役）。
    /// </summary>
    public class GrantToughnessHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantToughness;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.ToughnessCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.ToughnessCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"附加{effect.Value}层坚韧指示物（每次受到的伤害减{effect.Value}，生效后层数减半，可叠加）";
    }

    /// <summary>
    /// 圣盾原子（2026-10-08 指示物化，表行 c8624e6a——原子类型沿用 GrantDivineShield；
    /// refId 已重推 feab5d3b→c8624e6a，2026-10-08 全量 ID 重推·卡数据随后重建）：
    /// 对目标附加 {value} 层圣盾指示物（CounterRules.DivineShieldCounter）——每层抵挡一次任意伤害
    ///（战斗+效果）并消耗 1 层；可叠加（每层一份）；Entity 级（角色可持有）；
    /// 换区不清（生效自减档·2026-10-08 晚补裁决）、净化可清。
    /// 取代旧 GrantKeywordHandler 关键词路径（同轨取代制台账随之失效）。
    /// </summary>
    public class GrantDivineShieldHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantDivineShield;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.DivineShieldCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.DivineShieldCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"附加{effect.Value}层圣盾指示物（每层抵挡一次任意伤害后消耗）";
    }

    /// <summary>
    /// 复生原子（2026-10-08 指示物化，表行 94f6a32d——原子类型沿用 GrantReborn；
    /// refId 已重推 502be10d→94f6a32d，2026-10-08 全量 ID 重推·卡数据随后重建）：
    /// 对目标附加 {value} 层复生指示物（CounterRules.RebornCounter）——每层一次死亡替代
    ///（1 血回场横置，TryReborn 消耗 1 层）；可叠加；换区不清（生效自减档）、净化可清。
    /// </summary>
    public class GrantRebornHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantReborn;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.RebornCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.RebornCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"附加{effect.Value}层复生指示物（每层死亡时以1生命留场复活一次）";
    }

    /// <summary>
    /// 潜行原子（2026-10-08 指示物化，表行 8f84641e——原子类型沿用 GrantStealth；
    /// refId 已重推 a16f1e55→8f84641e，2026-10-08 全量 ID 重推·卡数据随后重建）：
    /// 对目标附加 {value} 层潜行指示物（CounterRules.StealthCounter）——持有期间不可被攻击/效果
    /// 指定（三指定口与隐密关键词同查）；攻击宣言/发动效果/实际受到伤害各消耗 1 层（逐份撤）。
    /// 换区不清（生效自减档）、净化可清。注意：隐密（Concealed）仍是关键词（持续版，不消耗）。
    /// </summary>
    public class GrantStealthHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantStealth;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.StealthCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.StealthCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"附加{effect.Value}层潜行指示物（不可被攻击和效果指定；攻击、发动效果或受到伤害后消耗1层）";
    }

    /// <summary>
    /// 法术护盾原子（2026-10-09 指示物化，表行 7270df35——原子类型沿用 GrantSpellShield；
    /// refId 已重推 a3ad6a04→7270df35，随指示物化文案换号）：对目标附加 {value} 层法术护盾指示物
    ///（CounterRules.SpellShieldCounter）——每层抵消一次对手效果对自身的作用（该效果执行时被移出
    /// 目标列表，ConsumeSpellShields 前置过滤消耗 1 层）；可叠加（每层一份）；
    /// 换区不清（生效自减档）、净化可清。取代旧 GrantKeywordHandler 关键词路径。
    /// </summary>
    public class GrantSpellShieldHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantSpellShield;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.SpellShieldCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.SpellShieldCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"附加{effect.Value}层法术护盾指示物（成为对手效果目标时抵消该效果并消耗1层）";
    }

    /// <summary>
    /// 毒素原子（例外类，2026-10-08 终版定案）：对目标附加 {value} 层毒素指示物——无持续时间、只有层数；
    /// 持有者每回合结束受到=层数的伤害，随后层数减半（向下取整，0.5→0）。层数可叠加（留存层持续伤害）。
    /// 例程在 CounterRules.OnTurnEnd ①（ProcessToxinException）；换区清、净化可清。
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
                target.AddCounters(CounterRules.ToxinCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.ToxinCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加{value}层毒素指示物（每回合结束受到等于层数的伤害后减半，无持续时间）";
    }

    /// <summary>
    /// 锁定原子（2026-10-04 窥渊仪典原子化定案，蓝2）：为一张手牌挂「锁定」指示物×{value}回合——
    /// 层数=剩余回合，持有者回合结束 −1（手牌区与场上同样倒数），归零解锁；
    /// 持有期间该牌无法使用（打出/响应出牌/苏醒立约同门——LockedCardRestriction + CommitAwaken 门）。
    /// </summary>
    public class LockCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LockCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // Value≤0 = 1 回合（缺省档，窥渊仪典按 1 回合赋予）
            int turns = context.GetValueAfterModifiers(effect.Value);
            if (turns <= 0) turns = 1;

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.LockCounter, turns, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.LockCounter,
                    Amount = turns,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加{value}回合锁定指示物（期间无法使用，持有者回合结束−1）";
    }

    /// <summary>
    /// 紊乱指示物（衰退类，2026-10-08 层即持续定案）：层=持续回合数，持有者回合末 −1、归零解除；
    /// 期间持有者不能以玩家为目标（攻击与效果发动同口径，TargetFilterSystem/CombatSystem 强制）。
    /// 长档紊乱已并入（同一机制不同量——教学 15 层即 15 回合）。
    /// </summary>
    public class RushSicknessHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RushSickness;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                // 层数=持续回合数：Value>0 时掷值（每目标独立，掷到 ≤0 = 空过），缺省 1 层
                int layers = effect.Value > 0
                    ? context.GetValueAfterModifiers(effect.GetRolledValue())
                    : 1;
                if (layers <= 0) continue;
                target.AddCounters(KeywordRules.RushSicknessCounter, layers, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.RushSicknessCounter,
                    Amount = layers,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加{value}回合紊乱指示物（期间不能以玩家为目标，持有者回合末倒数）";
    }

    /// <summary>
    /// 易损指示物（2026-10-09 生效自减定案，同毒素档，自 Decay 迁入）：无持续时间、只有层数——
    /// 每次受到伤害时每层使伤害 +1，生效后层数减半（floor，1 层生效一次即清零；KeywordRules.ApplyDamage
    /// 第 0 步放大+减半——圣盾/护甲吸收放大后的量）；换区不清、净化/解减益可清。
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

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加{value}层易损指示物（每次受到伤害每层+1，生效后层数减半）";
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
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个攻击力增加指示物";
    }

    /// <summary>
    /// 角色攻击力增加（2026-10-07 角色参战定案·弹药原子，Entity 级，生效自减类）：给己方角色附加 {value} 层
    /// 角色攻击指示物（HeroAttackCounter）——角色攻击力读数=层数（GetPower(Player) 直读），
    /// **攻击或反击结算后全部移除**（CounterRules.RemoveHeroAttackAmmo：攻击与反击共用同一份
    /// 弹药，烧完即止——弹药即闸门）。一般效果原子（任意时机挂载，"回合开始"是示例挂载）；
    /// 计价=指示物统一口径：基础费用×层数（2026-10-08 层改造定案）。
    /// 非 Player 目标跳过（表行 TargetFilter "Player" 收敛候选，此处防御双保险）。
    /// </summary>
    public class AddHeroAttackHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddHeroAttack;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (!(target is Player role) || !role.IsAlive) continue;
                role.AddCounters(CounterRules.HeroAttackCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = role,
                    CounterType = CounterRules.HeroAttackCounter,
                    Amount = stacks,
                    Source = context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"给角色附加{effect.Value}点攻击力（攻击或反击后移除）";
    }

    /// <summary>攻击力减少指示物（每层 −1 攻）</summary>
    public class AddPowerDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPowerDown;
        protected override string CounterId => CounterRules.PowerDownCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个攻击力减少指示物";
    }

    /// <summary>生命值增加指示物（每层 +1 上限与当前）</summary>
    public class AddLifeUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddLifeUp;
        protected override string CounterId => CounterRules.LifeUpCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个生命值增加指示物";
    }

    /// <summary>生命值减少指示物（每层 −1 上限，归零标死交 SBA）</summary>
    public class AddLifeDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddLifeDown;
        protected override string CounterId => CounterRules.LifeDownCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个生命值减少指示物";
    }

    /// <summary>属性增加指示物（每层 +1/+1，换区清除型）</summary>
    public class AddPlusOneHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPlusOne;
        protected override string CounterId => CounterRules.PlusOneCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个属性增加指示物（+1/+1）";
    }

    /// <summary>属性减少指示物（-1/-1）</summary>
    public class AddMinusOneHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddMinusOne;
        protected override string CounterId => CounterRules.MinusOneCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个属性减少指示物（-1/-1）";
    }

    /// <summary>虚弱：对{target}施加 {value} 个属性减少（= -1/-1 层 ×{value}）</summary>
    public class WeakenHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Weaken;
        protected override string CounterId => CounterRules.MinusOneCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"施加{effect.Value}个属性减少（-1/-1）";
    }

    /// <summary>鼓舞：对{target}施加 {value} 个属性增加（= +1/+1 层 ×{value}）</summary>
    public class InspireHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Inspire;
        protected override string CounterId => CounterRules.PlusOneCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"施加{effect.Value}个属性增加（+1/+1）";
    }

    /// <summary>费用增加指示物（每层 +1 费，仅手牌生效，离手消失）</summary>
    public class AddCostUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostUp;
        protected override string CounterId => CounterRules.CostUpCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个费用增加指示物";
    }

    /// <summary>费用减少指示物（每层 −1 费，仅手牌生效，离手消失）</summary>
    public class AddCostDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostDown;
        protected override string CounterId => CounterRules.CostDownCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"添加{effect.Value}个费用减少指示物";
    }

    /// <summary>
    /// 无效指示物（2026-09-09 定案，蓝3）：目标的非启动式能力无法发动——
    /// 拦全部触发式（含登场 OnPlay，挂 TriggerEngine.FindMatchingEffects）+ 拦光环静态能力
    /// （连接箭头来源被无效压制，LinkAuraSystem 查询时跳过——唯一能压光环的指示物；
    /// 净化/沉默不压箭头：箭头是卡面数据）。与沉默对称：沉默拦启动式、无效拦非启动式。
    /// </summary>
    public class AddNullifyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddNullify;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int amount = context.GetValueAfterModifiers(effect.Value);
                if (amount <= 0) amount = 1;
                target.AddCounters(CounterRules.NullifyCounter, amount, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.NullifyCounter,
                    Amount = amount,
                    Source = context.Source
                });
                PublishEvent(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = "无效",
                    Detail = "无效：持有者的非启动式能力（触发式/光环）无法发动",
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加无效指示物（非启动式能力无法发动）";
    }

    /// <summary>
    /// 沉睡原子（2026-09-11 定案，绿1 中性；2026-10-08 层即持续定案）：赋予目标沉睡指示物并横置——
    /// 层=持续回合数，持有者回合末 −1、归零解除（无法重置+效果无效期间持续）。
    /// 效果/指示物分离原则：本原子只负责**赋予指示物**——持续规则由指示物自身承载
    ///（CounterRules.OnTurnEnd ③块倒数；TriggerEngine/CanActivate 拦效果）。
    /// 自我沉睡的灰费转时长：目标=来源卡自身且 Value 未显式 → 数量=PendingSleepGray
    /// （打出时灰份额豁免量，GetCardCost 剥离暂存，消费即清）。
    /// </summary>
    public class SleepHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Sleep;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;

                // 2026-09-13 数值随机：层数掷值（每目标独立）；缺省 0 层走灰费豁免/兜底 1
                int amount = context.GetValueAfterModifiers(effect.GetRolledValue());
                if (amount <= 0 && context.Source is Card src && target == src && src.PendingSleepGray > 0)
                {
                    amount = src.PendingSleepGray; // 灰费豁免转时长（自我沉睡）
                    src._pendingSleepGray = 0;     // 消费即清（一次性）
                }
                if (amount <= 0)
                {
                    // 显式掷值到 ≤0 = 空过（不横置不挂层）；缺省（名义无层数）兜底 1 层
                    if (effect.Value > 0 && effect.RandomAmplitude > 0f) continue;
                    amount = 1;
                }

                // 沉睡=横置进沉睡（横置即上限：不能攻击/守卫；重置被指示物拦）。
                // 不走 ShouldTap（警戒 2026-09-10 已重定义为「横置也能反击」，无横置抵扣）。
                target.Tap();
                target.AddCounters(KeywordRules.SleepCounter, amount, context.Source);
                PublishEvent(new TapEvent { TappedEntity = target, IsUntapping = false });
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.SleepCounter,
                    Amount = amount,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => effect.Value > 0 ? $"赋予{effect.Value}层沉睡（无法重置、效果无效）" : "赋予沉睡（灰费豁免量=持续回合）";
    }
}
