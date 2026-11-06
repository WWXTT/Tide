using System;
using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // 2026-10-31 晚改定（用户）：指示物一次发动按原子 value 给层（≤0 或未填兜底 1）——
    // 原子表_指示物文案已回 {value}层 口径；系统指示物（CounterClass.System）恒 1 层不叠加
    //（CounterRules.ResolveLayers 单源）。例外：弹药原子 AddHeroAttack 层数照读 Value。
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
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int amount = CounterRules.ResolveLayers(KeywordRules.ArmorCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(KeywordRules.ArmorCounter, amount, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.ArmorCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "添加一层护甲指示物";
    }

    /// <summary>
    /// 坚韧原子（2026-10-08 指示物化定案，表行 a8f7f476——原子类型 GrantToughness，原 GrantArmor 同日更名；
    /// refId 迁移 05485f65→a312b8b0→758a74e0（同日哈希对齐）→2b1e3700（2026-10-09 生效自减改版随文案换号）
    /// →a8f7f476（现行号，2026-10-13 复核））：
    /// 对目标附加 {value} 层坚韧指示物（CounterRules.ToughnessCounter）——每次受到的伤害每层 −1，
    /// 实际拦到即生效、生效后层数减半（floor）、可叠加；Entity 级（角色可持有）；换区不清、净化可清。
    /// 取代旧 GrantKeywordHandler 关键词路径（台账 Value/Limit+次数闸+光环份额随之退役）。
    /// </summary>
    public class GrantToughnessHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantToughness;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.ToughnessCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.ToughnessCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.ToughnessCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层坚韧指示物（每次受到的伤害每层减1，生效后层数减半）";
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
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.DivineShieldCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.DivineShieldCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.DivineShieldCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层圣盾指示物（抵挡一次任意伤害后消耗）";
    }

    /// <summary>
    /// 复生原子（2026-10-08 指示物化，表行 94f6a32d——原子类型沿用 GrantReborn；
    /// refId 已重推 502be10d→94f6a32d，2026-10-08 全量 ID 重推·卡数据随后重建）：
        /// 对目标附加 {value} 层复生指示物（CounterRules.RebornCounter）——每层一次死亡替代
        ///（1 血回场，TryReborn 消耗 1 层；2026-10-19 起 Entity 级——角色归零宣判前消耗）；
        /// 可叠加；换区不清（生效自减档）、净化可清。
    /// </summary>
    public class GrantRebornHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantReborn;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.RebornCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.RebornCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.RebornCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层复生指示物（死亡时以1生命留场复活一次）";
    }

    /// <summary>
    /// 潜行原子（2026-10-08 指示物化，表行 2addfe0c——原子类型沿用 GrantStealth）：
    /// 对目标附加 {value} 层潜行指示物（CounterRules.StealthCounter）——持有期间不可被攻击/效果
    /// 指定，但须己方场上另有未潜行友方单位/角色才生效（2026-11-02 改版：全员隐藏时潜行整体失效；
    /// 判定单源 EntityEffectExtensions.IsHiddenFromTargeting）；宣言攻击结算/守卫拦截/主动发动效果
    /// 各消耗 1 层（2026-11-02 改版——实际受伤不消耗，触发式效果不消耗）。
    /// 换区不清（生效自减档）、净化可清。注意：隐密（Concealed）仍是关键词（持续版，不消耗）。
    /// </summary>
    public class GrantStealthHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantStealth;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.StealthCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.StealthCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.StealthCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层潜行指示物（若己方场上有其他未潜行友方单位，持有期间不可被攻击和效果指定；宣言攻击、守卫或发动效果后消耗1层）";
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
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.SpellShieldCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.SpellShieldCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.SpellShieldCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层法术护盾指示物（成为对手效果目标时抵消该效果并消耗1层）";
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
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.ToxinCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.ToxinCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.ToxinCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加一层毒素指示物（每回合结束受到等于层数的伤害，生效后层数减半）";
    }

    /// <summary>
    /// 报复原子（2026-10-16 新增，红1/层，生效自减档）：对目标附加 {value} 层报复指示物
    ///（CounterRules.RetaliationCounter）——持有者实际受到伤害时对伤害来源造成等于层数的伤害，
    /// 生效后层数减半（floor，1 层生效一次即清零）；可叠加。Entity 级；换区不清、净化可清。
    /// 触发口=KeywordRules.ApplyDamage 管线尾段（吸血后、剧毒前——先减半再反击，互带几何衰减防互弹）。
    /// </summary>
    public class GrantRetaliationHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantRetaliation;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.RetaliationCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.RetaliationCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.RetaliationCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层报复指示物（受到伤害时对伤害来源造成等于层数的伤害，生效后层数减半）";
    }

    /// <summary>
    /// 锁定原子（2026-11-02 改版）：为一张手牌挂「锁定」指示物×{value}回合——
    /// 层数=剩余回合，持有者回合结束 −1（手牌区与场上同样倒数），归零解锁；
    /// 持有期间无法使用（打出/响应出牌同门——LockedCardRestriction 封锁），
    /// 但可作为地牌放置（CanServeAsLand 放行——2026-11-02 起放地不算绕道）。
    /// </summary>
    public class LockCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LockCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（层=锁定回合数，≤0/未填=1）
            int turns = CounterRules.ResolveLayers(CounterRules.LockCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.LockCounter, turns, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.LockCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加一层锁定指示物（可作为地牌放置，无法使用；持有者回合结束−1层）";
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
            // 2026-10-31 晚定案：一次发动按 value 给层（层=持续回合数，≤0/未填=1）
            int layers = CounterRules.ResolveLayers(KeywordRules.RushSicknessCounter, context.GetValueAfterModifiers(effect.Value));
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(KeywordRules.RushSicknessCounter, layers, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.RushSicknessCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加一层紊乱指示物（期间不能以玩家为目标，持有者回合结束时倒数）";
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
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.VulnerableCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.VulnerableCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.VulnerableCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加一层易损指示物（每次受到伤害每层+1，生效后层数减半）";
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
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；系统指示物恒 1——ResolveLayers 单源）
            int stacks = CounterRules.ResolveLayers(CounterId, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                CounterRules.AddStatCounter(card, CounterId, stacks, context.Source, effect.AnchorTint()); // tint=赋予粒子（2026-11-01）
            }
        }
    }

    /// <summary>攻击力增加指示物（每层 +1 攻）</summary>
    public class AddPowerUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPowerUp;
        protected override string CounterId => CounterRules.PowerUpCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层攻击力增加指示物";
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
            // 弹药原子不在指示物一族（表行仍带 {value}）：层数照读 Value
            int stacks = context.GetValueAfterModifiers(effect.Value);
            if (stacks <= 0) stacks = 1;

            foreach (var target in context.Targets)
            {
                if (!(target is Player role) || !role.IsAlive) continue;
                int actualDelta = role.AddCounters(CounterRules.HeroAttackCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = role,
                    CounterType = CounterRules.HeroAttackCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"给角色附加{effect.Value}点攻击力（攻击或反击后移除）";
    }

    /// <summary>
    /// 反疗原子（2026-10-09，黑2，表行 6b9c4b90；原名堕落·仅角色，同日用户改名放开）：
    /// 对{target}附加 {value} 层反疗指示物（DepravityCounter，Exception 生效自减）——
    /// 任意有生命单位（生物+角色，双方；表行 TargetKinds=己方单位,对方单位·无 Filter）下一次
    /// 受到的治疗改写为等量伤害并消耗 1 层（改写口=EntityEffectExtensions.Heal 咽喉；
    /// 满血可选=DamagedFilter 豁免）。
    /// </summary>
    public class GrantDepravityHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantDepravity;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.DepravityCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.DepravityCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.DepravityCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层反疗指示物（下一次受到的治疗改写为等量伤害并消耗一层）";
    }

    /// <summary>
    /// 重放原子（2026-11-02 改版：对玩家指示物，蓝5，表行 93648aa7）：对{target}（仅玩家——
    /// 表行域=双方单位 + 滤 Player，即双方角色）附加 {value} 层重放指示物（ReplayCounter，
    /// Exception 生效自减、无回合限制）——该玩家使用的下一张法术/主动效果结算后自动消耗 1 层
    /// 并重复一轮（效果行+代价行——「包括效果和代价」；重放不触发重放；
    /// 消耗口=EffectExecutor.ExecuteAsync 结算段）。非玩家目标跳过；旧单位挂载口径
    ///（2026-10-09 白4，挂场上单位）已随改版退役。
    /// </summary>
    public class GrantReplayHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantReplay;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.ReplayCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (!(target is Player player) || !player.IsAlive) continue; // 对玩家指示物——非玩家目标跳过
                int actualDelta = player.AddCounters(CounterRules.ReplayCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = player,
                    CounterType = CounterRules.ReplayCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层重放指示物（该玩家使用的下一张法术/主动效果将重复一次——包括效果和代价，重放不触发重放）";
    }

    /// <summary>
    /// 临时指示物原子（2026-10-10 回响临时指示物化，白2，表行 GrantTemporary）：对{target}附加
    /// {value} 层临时指示物（TempCounter，Exception 换区不清——标记跟卡跨区；回合末例程=
    /// CounterRules.OnTurnEnd ②块）——持有者回合结束时该卡**不在战场则从游戏中移除**（消失，
    /// 不进墓）；**在战场不生效也不倒数**（2026-10-12 生效自减改稿——层保留，转正退役）。
    /// 任意卡区（手牌/牌库/墓地/除外区，双方——
    /// 2026-10-10 表行域口径，例程扫描范围同步含除外区；角色非卡不可中）；墓地卡可中（死亡→出局
    /// 的反复活用法，故不做 IsAlive 过滤）；非卡目标跳过。回响复制体恒带 1 层（TempCopyRules 施加口，不走本 handler）。
    /// </summary>
    public class GrantTemporaryHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantTemporary;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.TempCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue; // 墓地/手牌卡可中；角色非卡跳过（IsAlive 不查——墓地卡合法落点）
                int actualDelta = card.AddCounters(CounterRules.TempCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = CounterRules.TempCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层遗忘指示物（持有者回合结束时不在战场则从游戏中移除）";
    }

    /// <summary>
    /// 「成长」原子（2026-10-11 资源重做定案：灵气复苏 ×2 翻倍改成长 +1，绿3，资源族；
    /// 2026-10-15：封顶退役）：① 效果控制者的地牌槽上限 **+value**（各自池生效不共享），可无限重复
    /// （ElementPool.AddLandCapBonus → GetLandCap 单点：地牌张数/支付浓度/出牌费用门槛/黑白钳同随）。
    /// ② 2026-10-31 改版·2026-11-01 修订：每 +1 上限就从牌库随机拉一张**带元素费用**的卡进元素池
    /// （不指定锁色——缺省=主色即份额最多色）。「带元素费用」=GetLandColorChoices 非空
    /// （卡面/各模式费 ∪ 主动效果锚价——单源，任意色费卡同入）；
    /// **2026-11-03 用户定案：按 value 一次拉多张**——value=2 就是 +2 上限并连拉 2 张（逐张走
    /// AddCardToPool 权威闸：禁地令/地牌资格/上限余位任一被拒即少拉，牌库无带费卡即停）。
    /// 拉进来的地**横置入场**（本回合周期不再产色）。
    /// 无目标原子（作用=效果控制者）。
    /// </summary>
    public class IncreaseLandCapHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.IncreaseLandCap;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var player = context.Controller;
            var pool = context.ElementPool ?? GameCore.Instance?.ElementPool;
            if (player == null || pool == null)
            {
                TideLog.Warn("[IncreaseLandCap] 无效果控制者或元素池，空转");
                return;
            }
            var zm = context.ZoneManager;
            if (zm == null) return;

            int times = Math.Max(1, effect?.Value ?? 1);   // 2026-11-03：按 value 叠加（+1 上限 = 拉 1 张）
            for (int i = 0; i < times; i++)
            {
                pool.AddLandCapBonus(player);              // 上限先加，腾出本次入池的槽位

                // 牌库随机取一张带元素费用的卡横置入池（GameRng 种子可复播——网络对拍/回放确定性同发现原子）
                var deck = zm.GetCards(player, Zone.Deck);
                if (deck == null || deck.Count == 0) return;
                var candidates = new List<Card>();
                foreach (var card in deck)
                    if (card != null && ElementPoolSystem.GetLandColorChoices(card).Count > 0)
                        candidates.Add(card);
                if (candidates.Count == 0) return;

                var pick = candidates[GameRng.Next(0, candidates.Count)];
                if (!pool.AddCardToPool(pick, player)) return; // 不指定锁色=缺省主色（份额最多色）

                // 横置入场：本回合周期不再产色
                var pooled = pool.GetPool(player).PooledCards.Find(pc => pc != null && pc.SourceCard == pick);
                if (pooled != null) pooled.IsTapped = true;

                zm.MoveCard(pick, player, Zone.Deck, Zone.ElementPool);
                pool.PublishPoolAdd(pick, player);
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"地牌上限+{Math.Max(1, effect?.Value ?? 1)}（无上限），并从牌库随机将等量带元素费用的卡横置加入元素池";
    }

    /// <summary>攻击力减少指示物（每层 −1 攻）</summary>
    public class AddPowerDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPowerDown;
        protected override string CounterId => CounterRules.PowerDownCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层攻击力减少指示物";
    }

    /// <summary>生命值增加指示物（每层 +1 上限与当前）</summary>
    public class AddLifeUpHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddLifeUp;
        protected override string CounterId => CounterRules.LifeUpCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层生命值增加指示物";
    }

    /// <summary>生命值减少指示物（每层 −1 上限，归零标死交 SBA）</summary>
    public class AddLifeDownHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddLifeDown;
        protected override string CounterId => CounterRules.LifeDownCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层生命值减少指示物";
    }

    /// <summary>
    /// 耐久增加原子（2026-11-03 新增，蓝0.5/层 表行「耐久增加」·指示物表）：对无生命单位
    ///（表域=双方无生命单位——结界/装备）附加 {value} 层耐久指示物（CounterRules.DurabilityCounter，
    /// Resident 常驻类）——耐久=受击/被动触发次数池（受击全额扣、归零送墓），加层即时扩池；
    /// 铭刻光环照沾 +1（非 System 类）；绝缘体清零闸不拦（耐久非沉睡/缄默/StatKind）。
    /// 非 Card 目标（角色等）跳过——表域已限无生命单位，此处防御双保险。
    /// </summary>
    public class AddDurabilityUpHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddDurabilityUp;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1；CounterRules.ResolveLayers）
            int stacks = CounterRules.ResolveLayers(CounterRules.DurabilityCounter, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                int actualDelta = card.AddCounters(CounterRules.DurabilityCounter, stacks, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.DurabilityCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                    // 2026-11-04 战报定案：延迟渲染不能现查——发布口定格变化后层数（加层即时扩池）
                    AfterNote = $"现有 {card.GetCounterCount(CounterRules.DurabilityCounter)}",
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "增加一层耐久";
    }

    // AddPlusOneHandler/AddMinusOneHandler（属性±1/+1 计数层）2026-11-02 退役——用户定案「只能分开加减」，
    // 表行（8bc5b925/9864d6df）+handler 同批删除；攻/血单轴指示物行承接种类。

    /// <summary>虚弱：对{target}施加 {value} 个属性减少（= -1/-1 层 ×{value}）</summary>
    public class WeakenHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Weaken;
        protected override string CounterId => CounterRules.MinusOneCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "施加一层属性减少（-1/-1）";
    }

    /// <summary>鼓舞：对{target}施加 {value} 个属性增加（= +1/+1 层 ×{value}）</summary>
    public class InspireHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Inspire;
        protected override string CounterId => CounterRules.PlusOneCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "施加一层属性增加（+1/+1）";
    }

    /// <summary>
    /// 四色费用指示物（2026-10-31 通用费用增减四色化——增减各 4 行表行，每层 ±1 对应色净费分量，
    /// GetCardCost 逐色套层钳 0；仅手牌生效、换区清除）。减四色复用充能已投放的
    /// CostDownGray/Red/Blue/Green 计数器；增四色为 CostUpGray/Red/Blue/Green（同档对称）。
    /// </summary>
    public class AddCostUpGrayHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostUpGray;
        protected override string CounterId => CounterRules.CostUpGrayCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层灰色费用增加指示物";
    }

    public class AddCostUpRedHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostUpRed;
        protected override string CounterId => CounterRules.CostUpRedCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层红色费用增加指示物";
    }

    public class AddCostUpBlueHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostUpBlue;
        protected override string CounterId => CounterRules.CostUpBlueCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层蓝色费用增加指示物";
    }

    public class AddCostUpGreenHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostUpGreen;
        protected override string CounterId => CounterRules.CostUpGreenCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层绿色费用增加指示物";
    }

    public class AddCostDownGrayHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostDownGray;
        protected override string CounterId => CounterRules.CostDownGrayCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层灰色费用减少指示物";
    }

    public class AddCostDownRedHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostDownRed;
        protected override string CounterId => CounterRules.CostDownRedCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层红色费用减少指示物";
    }

    public class AddCostDownBlueHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostDownBlue;
        protected override string CounterId => CounterRules.CostDownBlueCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层蓝色费用减少指示物";
    }

    public class AddCostDownGreenHandler : StatCounterAtomHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCostDownGreen;
        protected override string CounterId => CounterRules.CostDownGreenCounter;
        protected override string DescribeTemplate(AtomicEffectInstance effect) => "添加一层绿色费用减少指示物";
    }

    /// <summary>
    /// 失效指示物（2026-09-09 定案蓝3；2026-10-19 定名「缄默」，2026-10-31 改名「失效」，
    /// 运行时 id 沿用 Nullify）：持有者的自动效果失效——拦全部触发式（含登场 OnPlay，
    /// 挂 TriggerEngine.FindMatchingEffects）+ 拦本体关键词（EntityEffectExtensions.HasKeyword 收口）。
    /// 2026-10-31 改名定案：光环域整体退出——不压连接光环来源/规则光环载体（受益者侧本就不除光环）。
    /// 与沉默对称：沉默拦主动、失效拦自动。
    /// </summary>
    public class AddNullifyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddNullify;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1）
            int amount = CounterRules.ResolveLayers(CounterRules.NullifyCounter, context.GetValueAfterModifiers(effect.Value));
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.NullifyCounter, amount, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.NullifyCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint() // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
                PublishEvent(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = "失效",
                    Detail = "失效：持有者的自动效果（触发式与本体关键词）失效——不影响光环",
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加失效指示物（自动效果无法发动）";
    }

    /// <summary>
    /// 沉睡原子（2026-09-11 定案，绿1 中性；2026-10-08 层即持续定案）：赋予目标沉睡指示物并横置——
    /// 层=持续回合数，持有者回合末 −1、归零解除（无法重置+效果无效期间持续）。
    /// 效果/指示物分离原则：本原子只负责**赋予指示物**——持续规则由指示物自身承载
    ///（CounterRules.OnTurnEnd ③块倒数；TriggerEngine/CanActivate 拦效果）。
    /// 旧「豁免费转时长」消费口已删（2026-10-29 P6 苏醒改版；苏醒本身已于 2026-11-02 整链删除）；
    /// 本原子 Value 未显式时一律兜底 1 层。
    /// </summary>
    public class SleepHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Sleep;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1）
            int amount = CounterRules.ResolveLayers(KeywordRules.SleepCounter, context.GetValueAfterModifiers(effect.Value));
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;

                // 沉睡=横置进沉睡（横置即上限：不能攻击/守卫；重置被指示物拦）。
                // 不走 ShouldTap（警戒 2026-09-10 已重定义为「横置也能反击」，无横置抵扣）。
                target.Tap();
                int actualDelta = target.AddCounters(KeywordRules.SleepCounter, amount, context.Source);
                PublishEvent(new TapEvent { TappedEntity = target, IsUntapping = false });
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = KeywordRules.SleepCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint() // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "赋予一层沉睡（横置无法重置，自动效果无效）";
    }

    /// <summary>
    /// 无实体原子（2026-11-02 指示物化，表行 6c38edaa·EffectType 不变）：为目标附加
    /// {value} 层无实体指示物（CounterRules.SpellbanCounter，衰退类）——持有期间每次受到的伤害
    /// 固定变为 1（钳口=KeywordRules.ApplyDamage 守护改写之后）；层=持续回合（持有者回合末 −1），
    /// 不按次消耗。效果/指示物分离原则：本原子只负责赋予，持续规则由指示物自身承载。
    /// </summary>
    public class GrantSpellbanHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantSpellban;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 一次发动按 value 给层（≤0/未填=1；层=持续回合，衰退类）
            int layers = CounterRules.ResolveLayers(CounterRules.SpellbanCounter, context.GetValueAfterModifiers(effect.Value));
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.SpellbanCounter, layers, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.SpellbanCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint() // 赋予粒子（2026-11-01）：表现层以非 null 识别「赋予」并按极性×颜色播爆发
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "附加一层无实体指示物（持有期间每次受到的伤害变为1）";
    }
}
