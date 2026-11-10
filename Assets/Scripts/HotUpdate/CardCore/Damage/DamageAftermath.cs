using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Damage
{
    // =====================================================================================
    // 伤害后效（2026-11-09《伤害管线与循环链接口草案》Phase 3：抽 Aftermath）。
    //
    // 铁律（§4.3）：
    //   · Aftermath 不许直接调 ApplyDamage——只 Enqueue（经 DamageChain 门禁）；
    //   · 规则顺序由 Priority 声明（100 事件 → 200 吸血/系命 → 300 荆棘 → 400 报复
    //     → 500 病原体 → 600 剧毒），与 Legacy 管线尾段时序一致；
    //   · 报复/荆棘的后续伤害是新的 DamageRequest，走完整管线（对方防护照常拦截）；
    //   · 致死伤害的 Aftermath 仍执行（濒死反咬）；已入队事件允许 Source 已死。
    // 事件文案逐行平移 Legacy；唯一已裁决修正（§6.1.1）：报复反噬 KeywordAppliedEvent
    // 的 Source 字段改填伤害来源（Legacy 误填反噬者，语义反了）。
    // =====================================================================================

    /// <summary>后效规则（§4.1）。Applies 只看结果；副作用经 IAftermathContext 出口。</summary>
    public interface IAftermathRule
    {
        int Priority { get; }
        bool Applies(DamageResult result);
        void Apply(DamageResult result, IAftermathContext ctx);
    }

    /// <summary>
    /// 后效出口（§4.1）。Enqueue 必须先过 DamageChain.CanEnqueue——OneBounce 兼容档
    /// 在此拦截，不在各关键词里各写 guard；返回 false = 被链门拦下（调用方据此不发事件）。
    /// </summary>
    public interface IAftermathContext
    {
        DamageChain Chain { get; }
        void Publish(IGameEvent evt);
        void Heal(Entity target, int amount, Entity source);
        void AddCounter(Entity target, string counterId, int amount, Entity source);
        bool Enqueue(FollowUpEvent followUp);
        void MarkLethal(Entity target, DeathCause cause, Entity source);
    }

    /// <summary>后效引擎：按 Priority 串行过规则。</summary>
    public interface IAftermathEngine
    {
        void Process(DamageResult result, ChainContext chainCtx);
    }

    public sealed class AftermathEngine : IAftermathEngine
    {
        private readonly List<IAftermathRule> _rules;

        public AftermathEngine(IEnumerable<IAftermathRule> rules)
        {
            _rules = rules.OrderBy(r => r.Priority).ToList();
        }

        public void Process(DamageResult result, ChainContext chainCtx)
        {
            var ctx = new AftermathContext(chainCtx != null ? chainCtx.Chain : null);
            foreach (var rule in _rules)
            {
                if (rule.Applies(result))
                    rule.Apply(result, ctx);
            }
        }
    }

    internal sealed class AftermathContext : IAftermathContext
    {
        private readonly DamageChain _chain;

        public AftermathContext(DamageChain chain) { _chain = chain; }

        public DamageChain Chain => _chain;

        public void Publish(IGameEvent evt)
        {
            if (evt == null) return;
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(evt);
            else EventManager.Instance.PublishDynamic(evt);
        }

        public void Heal(Entity target, int amount, Entity source) => target?.Heal(amount);

        public void AddCounter(Entity target, string counterId, int amount, Entity source)
            => target?.AddCounters(counterId, amount, source);

        /// <summary>链门禁：OneBounce 在 DamageChain.CanEnqueue 拦（返回 false 不入队）。</summary>
        public bool Enqueue(FollowUpEvent followUp)
        {
            if (_chain == null) return false;
            return _chain.Enqueue(followUp);
        }

        /// <summary>致死锁存（§3.4）：Legacy 同款标记，SBA checkpoint 消费。</summary>
        public void MarkLethal(Entity target, DeathCause cause, Entity source)
        {
            if (target is Card card && card.IsAlive)
            {
                card._life = 0;
                card._pendingDeathCause = cause;
                card._pendingDeathSource = source;
                card.IsAlive = false;
            }
        }
    }

    /// <summary>默认规则集装配（顺序即 Priority）。</summary>
    public static class DamageAftermathRules
    {
        public static IAftermathRule[] CreateDefault() => new IAftermathRule[]
        {
            new EventPublicationRule(),
            new LifestealRule(),
            new ThornsRule(),
            new RetaliateRule(),
            new PathogenRule(),
            new VenomRule(),
        };
    }

    // ------------------------------ 100 事件发布 ------------------------------

    /// <summary>事件链（Legacy 管线尾段时序平移）：DamageEvent（路由，替代已消费）→
    /// CombatDamageEvent（战斗）→ LifeChangeEvent（角色）。dealt=0 不发布（全挡提前出管线）。</summary>
    public sealed class EventPublicationRule : IAftermathRule
    {
        public int Priority => 100;

        public bool Applies(DamageResult r) => r.Dealt > 0;

        public void Apply(DamageResult r, IAftermathContext ctx)
        {
            var damageEvent = new DamageEvent { Source = r.Source, Target = r.FinalTarget, Amount = r.Dealt };
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEventRouted(damageEvent, replacementsApplied: true);
            else
                EventManager.Instance.Publish(damageEvent);

            if (r.Kind == DamageKind.Combat)
            {
                EventManager.Instance.Publish(new CombatDamageEvent
                {
                    Attacker = r.Source,
                    Defender = r.FinalTarget,
                    Damage = r.Dealt,
                });
            }
            if (r.FinalTarget is Player hurt)
            {
                EventManager.Instance.Publish(new LifeChangeEvent
                {
                    Player = hurt,
                    OldLife = r.OldLife,
                    NewLife = hurt.Life,
                    Source = r.Source,
                });
            }
        }
    }

    // ------------------------------ 200 吸血/系命 ------------------------------

    /// <summary>吸血（回复随从自身）/系命（回复角色）：按实际造成量回复（dealt 口径 2026-10-31 阶段B）。
    /// 来源已死不回复（Legacy 同门）；不回 LoopCost.Life（血债是 LifePayment，不进伤害事件——§4.3）。</summary>
    public sealed class LifestealRule : IAftermathRule
    {
        public int Priority => 200;

        public bool Applies(DamageResult r)
            => r.Dealt > 0 && r.Source != null && r.Source.IsAlive;

        public void Apply(DamageResult r, IAftermathContext ctx)
        {
            if (r.Source.HasKeyword(KeywordRules.Lifesteal) && r.Source is Card stealer && CardEffectBudget.KeywordAllowed(stealer))
            {
                ctx.Heal(stealer, r.Dealt, r.FinalTarget);
                CardEffectBudget.CommitKeyword(stealer);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = stealer,
                    Keyword = KeywordRules.Lifesteal,
                    Detail = $"吸血回复 {r.Dealt} 生命",
                    Source = r.FinalTarget,
                });
            }
            if (r.Source.HasKeyword(KeywordRules.Lifelink) && r.Source.GetController() is Player owner && CardEffectBudget.KeywordAllowed(r.Source))
            {
                owner.Life += r.Dealt;
                CardEffectBudget.CommitKeyword(r.Source);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = owner,
                    Keyword = KeywordRules.Lifelink,
                    Detail = $"系命回复 {r.Dealt} 生命",
                    Source = r.Source,
                });
            }
        }
    }

    // ------------------------------ 300 荆棘 ------------------------------

    /// <summary>荆棘（RetaliationCounter，2026-11-09 改名原「报复」）：实际受到伤害时对来源
    /// 造成=层数的伤害，先减半层数再反击（Halve 策略；互带几何衰减天然收敛）。
    /// 后续伤害=新 DamageRequest 走完整管线（Legacy isCombat:false 平移）。</summary>
    public sealed class ThornsRule : IAftermathRule
    {
        public int Priority => 300;

        public bool Applies(DamageResult r)
            => r.Dealt > 0
               && r.Source != null
               && r.Source.IsAlive
               && !ReferenceEquals(r.Source, r.FinalTarget);

        public void Apply(DamageResult r, IAftermathContext ctx)
        {
            int layers = r.FinalTarget.GetEffectiveCounterCount(CounterRules.RetaliationCounter);
            if (layers <= 0) return;

            int halved = layers / 2;
            r.FinalTarget.AddCounters(CounterRules.RetaliationCounter, -(layers - halved));
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = r.FinalTarget,
                Keyword = CounterRules.RetaliationCounter,
                Detail = $"荆棘反噬：{EffectText.Name(r.FinalTarget)} 对 {EffectText.Name(r.Source)} 造成 {layers} 点伤害（生效减半，余 {halved} 层）",
                Source = r.FinalTarget,
            });

            ctx.Enqueue(new FollowUpEvent
            {
                Request = new DamageRequest
                {
                    Source = r.FinalTarget, // 反噬者
                    Target = r.Source,
                    Amount = layers,        // 等于当前层数（减半前）
                    Kind = DamageKind.Effect,
                    Tags = DamageTags.Chain,
                    Origin = r.FinalTarget,
                },
                Policy = new ChainPolicy
                {
                    Loop = LoopPolicy.Halve, // 荆棘靠层数几何衰减收敛（不付血债、不用 Visited）
                    Cost = LoopCost.None,
                    AllowRevisit = true,
                    StopOnStateRepeat = false, // 层数衰减中状态天然不重复；开检测反误伤
                },
                Owner = r.FinalTarget,
                Origin = r.FinalTarget,
                Keyword = CounterRules.RetaliationCounter,
                Generation = r.ChainDepth + 1,
            });
        }
    }

    // ------------------------------ 400 报复 ------------------------------

    /// <summary>报复关键词（2026-11-09 新增，红5 GrantRetaliate）：实际受到伤害时对来源
    /// 反噬等量（=dealt）伤害。策略继承根链（§6.1.1）：旧卡/兼容档 OneBounce+无血债
    ///（等价 Legacy _retaliateBounceGuard——CanEnqueue 拦 Generation>1）；新卡 UntilDeath
    /// +Life(1) 血债（设计裁决：让循环以可见代价自然收束）。
    /// 事件口径修正：Source 填伤害来源（Legacy 误填反噬者）；只有真正入队才发事件
    ///（保证 OneBounce 兼容档与 Legacy 的事件数一致）。</summary>
    public sealed class RetaliateRule : IAftermathRule
    {
        public int Priority => 400;

        public bool Applies(DamageResult r)
            => r.Dealt > 0
               && r.Source != null
               && r.Source.IsAlive               // 来源已死不反噬（Legacy 同门）
               && !ReferenceEquals(r.Source, r.FinalTarget) // 自伤不反噬
               && r.FinalTarget.HasKeyword(KeywordRules.Retaliate)
               && CardEffectBudget.KeywordAllowed(r.FinalTarget); // §11 高危：过载不触发
        // 不检查 FinalTarget.IsAlive——致死伤害的报复仍入队（濒死反咬，§5.5）

        public void Apply(DamageResult r, IAftermathContext ctx)
        {
            var root = ctx.Chain != null ? ctx.Chain.Policy : null;
            bool oneBounce = root != null && root.Loop == LoopPolicy.OneBounce;
            CardEffectBudget.CommitKeyword(r.FinalTarget); // 入队即视为已触发（§11.3 结算后计）

            var followUp = new FollowUpEvent
            {
                Request = new DamageRequest
                {
                    Source = r.FinalTarget,   // 反噬者
                    Target = r.Source,        // 伤害来源
                    Amount = r.Dealt,         // 等量
                    Kind = DamageKind.Effect,
                    Tags = DamageTags.Retaliate | DamageTags.Chain,
                    Origin = r.FinalTarget,
                },
                Policy = new ChainPolicy
                {
                    Loop = oneBounce ? LoopPolicy.OneBounce : LoopPolicy.UntilDeath,
                    // OneBounce 与 Legacy 完全一致：不付血债；UntilDeath 继承根策略 Cost
                    //（默认 Life(1) 血债；§6.5 真无限档=根策略配 None，子事件照继承）；
                    // 边链（ChainBudget）等其它根不继承——报复自带 Life(1) 兜底，
                    // 否则边内互弹无血债=跑满预算。
                    Cost = oneBounce ? LoopCost.None
                         : (root != null && (root.Loop == LoopPolicy.OneBounce || root.Loop == LoopPolicy.UntilDeath)
                                ? root.Cost
                                : LoopCost.Life(1)),
                    AllowRevisit = true,      // 不靠 Visited 挡互弹
                    StopOnStateRepeat = root == null || root.StopOnStateRepeat,
                    MaxEvents = root != null ? root.MaxEvents : 256,
                    MaxDepth = root != null ? root.MaxDepth : 64,
                },
                Owner = r.FinalTarget,        // 血债由反噬者支付
                Origin = r.FinalTarget,
                Keyword = KeywordRules.Retaliate,
                Generation = r.ChainDepth + 1,
            };

            if (!ctx.Enqueue(followUp)) return; // OneBounce 兼容档在链门被拦——不发事件（与 Legacy 事件数一致）

            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = r.FinalTarget,
                Keyword = KeywordRules.Retaliate,
                Detail = $"报复反噬：{EffectText.Name(r.FinalTarget)} 对 {EffectText.Name(r.Source)} 造成 {r.Dealt} 点伤害（等量）",
                Source = r.Source, // 事件口径修正（§6.1.1）：Source=伤害来源，Legacy 误填反噬者
            });
        }
    }

    // ------------------------------ 500 病原体 ------------------------------

    /// <summary>病原体：持有者实际受到伤害时对来源附加 1 层毒素（Legacy 6.6 段平移；自伤照染）。</summary>
    public sealed class PathogenRule : IAftermathRule
    {
        public int Priority => 500;

        public bool Applies(DamageResult r)
            => r.Dealt > 0 && r.Source != null && r.Source.IsAlive
               && r.FinalTarget.HasKeyword(KeywordRules.Pathogen)
               && CardEffectBudget.KeywordAllowed(r.FinalTarget); // §11 高危：过载不触发

        public void Apply(DamageResult r, IAftermathContext ctx)
        {
            int delta = r.Source.AddCounters(CounterRules.ToxinCounter, 1, r.FinalTarget);
            CardEffectBudget.CommitKeyword(r.FinalTarget);
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = r.Source,
                CounterType = CounterRules.ToxinCounter,
                Amount = delta,
                Source = r.FinalTarget,
            });
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = r.Source,
                Keyword = KeywordRules.Pathogen,
                Detail = $"病原体：{EffectText.Name(r.FinalTarget)} 受到伤害，{EffectText.Name(r.Source)} 感染 {delta} 层毒素",
                Source = r.FinalTarget,
            });
        }
    }

    // ------------------------------ 600 剧毒 ------------------------------

    /// <summary>剧毒：受到其战斗伤害的生物结算后被消灭（TryKill 消灭口径；伤害已致死不重复——
    /// Legacy venomTarget.IsAlive 门平移，链上致死=IsAlive 已翻 false，同门拦截）。</summary>
    public sealed class VenomRule : IAftermathRule
    {
        public int Priority => 600;

        public bool Applies(DamageResult r)
            => r.Kind == DamageKind.Combat
               && r.Dealt > 0
               && r.Source != null
               && r.Source.IsAlive
               && r.FinalTarget is Card venomTarget
               && venomTarget.IsAlive
               && !venomTarget.IsNonLivingUnit()
               && r.Source.HasKeyword(KeywordRules.Venom)
               && CardEffectBudget.KeywordAllowed(r.Source); // §11 高危：过载不触发

        public void Apply(DamageResult r, IAftermathContext ctx)
        {
            DeathRules.TryKill(r.FinalTarget as Card, DeathCause.DestroyEffect, r.Source,
                GameCore.Instance?.ZoneManager);
            CardEffectBudget.CommitKeyword(r.Source);
        }
    }
}
