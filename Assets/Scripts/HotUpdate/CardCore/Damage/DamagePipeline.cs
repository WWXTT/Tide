using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Damage
{
    // =====================================================================================
    // 单事件伤害管线（2026-11-09《伤害管线与循环链接口草案》Phase 2：抽 Pipeline）。
    //
    // 层序（§2.1，规则契约而非实现细节）：
    //   100 Target（ITargetValidator，非注册 Stage）→ 200 Replace（替代引擎）→
    //   300 Redirect（守护改写，单跳）→ 400 Amplify（无实体钳/暴击，防护层之前）→
    //   500 Prevent（免疫/减半/圣盾/护甲/坚韧）→ 600 FinalModify（易损，防护层之后、
    //   全挡跳过）→ 700 Apply（落血/耐久/生命锁/致死锁存）。
    //
    // 与 Legacy（KeywordRules.ApplyDamageLegacy）的两处已裁决差异（§2.4/§7.4，记录在案）：
    //   ① 暴击（Amplify）先于减半护盾（Prevent）：Legacy 是减半→暴击，取整结果不同；
    //   ② 守护改写后不重跑替代（RedirectPolicy.ReapplyReplacements=false）：
    //      Legacy 的守护递归会整段重入（含替代复查）。
    // 其余段的算法/事件文案均逐行平移 Legacy。
    // =====================================================================================

    /// <summary>层序常量（对外契约；Target 只作顺序标签/日志，实体是 Resolve 入口处的校验器）。</summary>
    public static class DamageStageOrder
    {
        public const int Target = 100;
        public const int Replace = 200;
        public const int Redirect = 300;
        public const int Amplify = 400;
        public const int Prevent = 500;
        public const int FinalModify = 600;
        public const int Apply = 700;
    }

    /// <summary>单事件上下文（草案 §3.1）。Replace/Redirect 改目标后置 TargetDirty，由管线在下一层前复检。</summary>
    public sealed class DamageContext
    {
        public DamageRequest Request;
        public Entity Source;
        public Entity Target;
        public int Amount;
        public int Prevented;
        public int Dealt;
        public int OldLife;          // 角色落血前生命（LifeChangeEvent 播报用）
        public int ReplacementDepth;
        public int RedirectDepth;
        public bool TargetDirty;
        public bool Fizzled;
        public bool Lethal;

        public DamageResult ToResult(int chainDepth)
        {
            var result = new DamageResult
            {
                Source = Source,
                FinalTarget = Target,
                Requested = Request != null ? Request.Amount : 0,
                Dealt = Dealt,
                Prevented = Prevented,
                Lethal = Lethal,
                Fizzled = Fizzled,
                OldLife = OldLife,
                Kind = Request != null ? Request.Kind : DamageKind.Effect,
                ChainDepth = chainDepth,
            };
            return result;
        }
    }

    /// <summary>目标校验器（§2.1）：入口与 TargetDirty 复检共用同一口径。</summary>
    public interface ITargetValidator
    {
        bool IsValid(DamageContext ctx);
    }

    /// <summary>管线层（§2.1 不变量 1：每个效果声明 Stage+Priority，禁止物理段序）。</summary>
    public interface IDamageStage
    {
        int Order { get; }
        void Apply(DamageContext ctx);
    }

    /// <summary>
    /// 管线本体：入口校验 → 按层序过 Stage；Replace/Redirect 改目标后、下一层前复检
    ///（不重跑 Replace，防替代环——§2.1 不变量 2）。Stage 不得递归调 ApplyDamage（不变量 6）。
    /// </summary>
    public sealed class DamagePipeline
    {
        private readonly List<IDamageStage> _stages = new List<IDamageStage>();
        private readonly ITargetValidator _targetValidator;

        public DamagePipeline(ITargetValidator targetValidator)
        {
            _targetValidator = targetValidator;
        }

        public void Register(IDamageStage stage)
        {
            _stages.Add(stage);
            _stages.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        /// <summary>已注册层（层序断言/诊断用，只读暴露）。</summary>
        public IEnumerable<IDamageStage> Stages => _stages;

        public DamageResult Resolve(DamageRequest request, int chainDepth = 0)
        {
            var ctx = new DamageContext
            {
                Request = request,
                Source = request != null ? request.Source : null,
                Target = request != null ? request.Target : null,
                Amount = request != null ? request.Amount : 0,
            };

            // Legacy 入口三闸平移：非正量/空目标/已死目标 → 0（不产事件）
            if (request == null || request.Amount <= 0)
            {
                ctx.Fizzled = true;
                return ctx.ToResult(chainDepth);
            }
            if (!_targetValidator.IsValid(ctx))
            {
                ctx.Fizzled = true;
                return ctx.ToResult(chainDepth);
            }

            foreach (var stage in _stages)
            {
                if (ctx.Fizzled) break;

                if (ctx.TargetDirty)
                {
                    ctx.TargetDirty = false;
                    if (!_targetValidator.IsValid(ctx))
                    {
                        ctx.Fizzled = true; // 替代/转移改写出的非法目标 → fizzle（Legacy 缺此复检，§2.2.2 修复）
                        break;
                    }
                }

                stage.Apply(ctx);
            }

            return ctx.ToResult(chainDepth);
        }
    }

    /// <summary>默认目标校验：非空且存活（与 Legacy 入口同口径，复检时复用）。</summary>
    public sealed class DefaultTargetValidator : ITargetValidator
    {
        public bool IsValid(DamageContext ctx)
            => ctx.Target != null && ctx.Target.IsAlive;
    }

    // ============================== 200 Replace ==============================

    public interface IDamageReplacement
    {
        int Priority { get; }
        bool CanReplace(DamageContext ctx);
        void Replace(DamageContext ctx); // 可改 Amount / Target
    }

    /// <summary>替代层（§3.2）：深度上限只做断言；改目标后置 TargetDirty 由管线复检。</summary>
    public sealed class ReplaceStage : IDamageStage
    {
        public const int MaxDepth = 4;
        private readonly List<IDamageReplacement> _replacements;

        public int Order => DamageStageOrder.Replace;

        public ReplaceStage(IEnumerable<IDamageReplacement> replacements)
        {
            _replacements = replacements.ToList();
        }

        public void Apply(DamageContext ctx)
        {
            foreach (var r in _replacements.OrderBy(x => x.Priority))
            {
                if (ctx.ReplacementDepth >= MaxDepth) break;
                if (!r.CanReplace(ctx)) continue;

                r.Replace(ctx);
                ctx.ReplacementDepth++;
                ctx.TargetDirty = true; // 改数量时复检也无害；改目标时必须（草案 §3.2）

                if (ctx.Amount <= 0)
                {
                    ctx.Fizzled = true; // 替代归 0：提前出管线、不产事件（Legacy 同口径）
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 替代引擎适配器：把 GameCore.ReplacementEngine 的 DamageEvent 检查接进 Replace 层。
    /// 逐行平移 Legacy 管线头段（含替代件改写承受者——血偿转嫁）。
    /// </summary>
    public sealed class ReplacementEngineAdapter : IDamageReplacement
    {
        public int Priority => 0;

        public bool CanReplace(DamageContext ctx)
            => GameCore.Instance != null && GameCore.Instance.ReplacementEngine != null;

        public void Replace(DamageContext ctx)
        {
            var routedEvent = new DamageEvent { Source = ctx.Source, Target = ctx.Target, Amount = ctx.Amount };
            var engine = GameCore.Instance.ReplacementEngine;
            if (engine.CheckReplacements(routedEvent).GetFinalEvent() is DamageEvent final)
            {
                ctx.Amount = final.Amount;
                if (final.Target != null && !ReferenceEquals(final.Target, ctx.Target))
                    ctx.Target = final.Target; // 替代件改写承受者（血偿转嫁）
            }
        }
    }

    // ============================== 300 Redirect ==============================

    /// <summary>转移策略（§3.2）：默认单跳守护改写；不重跑替代（与 Legacy 守护递归的已裁决差异）。</summary>
    public sealed class RedirectPolicy
    {
        public int MaxDepth = 1;
        public bool RevalidateAfterRedirect = true;
        public bool ReapplyReplacements = false; // 预留位：true = 改写后重跑 Replace（v1 恒 false，§2.4 记录差异）
    }

    public interface IDamageRedirect
    {
        int Priority { get; }
        bool TryRedirect(DamageContext ctx, out Entity newTarget);
    }

    public sealed class RedirectStage : IDamageStage
    {
        private readonly List<IDamageRedirect> _redirects;
        private readonly RedirectPolicy _policy;

        public int Order => DamageStageOrder.Redirect;

        public RedirectStage(IEnumerable<IDamageRedirect> redirects, RedirectPolicy policy)
        {
            _redirects = redirects.ToList();
            _policy = policy;
        }

        public void Apply(DamageContext ctx)
        {
            while (ctx.RedirectDepth < _policy.MaxDepth)
            {
                IDamageRedirect hit = null;
                Entity next = null;
                foreach (var r in _redirects.OrderBy(x => x.Priority))
                {
                    if (r.TryRedirect(ctx, out var candidate))
                    {
                        hit = r;
                        next = candidate;
                        break;
                    }
                }

                if (hit == null) break;

                ctx.Target = next;
                ctx.RedirectDepth++;
                if (_policy.RevalidateAfterRedirect)
                    ctx.TargetDirty = true;
            }
        }
    }

    /// <summary>守护改写（GuardianRules 单跳平移）：角色受伤改写为该侧守护者承受；事件文案同 Legacy。</summary>
    public sealed class GuardianRedirect : IDamageRedirect
    {
        public int Priority => 0;

        public bool TryRedirect(DamageContext ctx, out Entity newTarget)
        {
            newTarget = null;
            var guardian = GuardianRules.FindGuardian(ctx.Target);
            if (guardian == null || ReferenceEquals(guardian, ctx.Target)) return false;

            // §11.2.1 OnTargeted 白名单（计入不压——守护是静态规则，过载不拦改写；只计数）
            CardEffectBudget.CommitKeyword(guardian);

            if (GameCore.Instance != null)
                GameCore.Instance.PublishEvent(new KeywordAppliedEvent
                {
                    Target = ctx.Target,
                    Keyword = KeywordRules.Guardian,
                    Detail = $"守护改写：{EffectText.Name(ctx.Target)} 受到的伤害转由 {EffectText.Name(guardian)} 承受",
                    Source = guardian,
                });

            newTarget = guardian;
            return true;
        }
    }

    // ============================== 400 Amplify / 600 FinalModify ==============================

    public interface IDamageModifier
    {
        int Stage { get; }      // Amplify / FinalModify
        int Priority { get; }
        bool Applies(DamageContext ctx);
        void Apply(DamageContext ctx);
    }

    public sealed class AmplifyStage : IDamageStage
    {
        private readonly List<IDamageModifier> _mods;
        public int Order => DamageStageOrder.Amplify;

        public AmplifyStage(IEnumerable<IDamageModifier> mods) { _mods = mods.ToList(); }

        public void Apply(DamageContext ctx)
        {
            foreach (var m in _mods.Where(x => x.Stage == DamageStageOrder.Amplify).OrderBy(x => x.Priority))
                if (m.Applies(ctx)) m.Apply(ctx);
        }
    }

    public sealed class FinalModifyStage : IDamageStage
    {
        private readonly List<IDamageModifier> _mods;
        public int Order => DamageStageOrder.FinalModify;

        public FinalModifyStage(IEnumerable<IDamageModifier> mods) { _mods = mods.ToList(); }

        public void Apply(DamageContext ctx)
        {
            if (ctx.Amount <= 0) return; // 全挡默认不进入最终修改（易损不放大、不消耗——§2.1 不变量 4）
            foreach (var m in _mods.Where(x => x.Stage == DamageStageOrder.FinalModify).OrderBy(x => x.Priority))
                if (m.Applies(ctx)) m.Apply(ctx);
        }
    }

    /// <summary>无实体（Spellban 指示物）：受到的伤害固定为 1（防护层之前的数值改写，Legacy 段序平移）。</summary>
    public sealed class SpellbanClampModifier : IDamageModifier
    {
        public int Stage => DamageStageOrder.Amplify;
        public int Priority => 10;

        public bool Applies(DamageContext ctx)
            => ctx.Amount > 1 && ctx.Target.GetEffectiveCounterCount(CounterRules.SpellbanCounter) > 0;

        public void Apply(DamageContext ctx)
        {
            ctx.Amount = 1;
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ctx.Target,
                Keyword = CounterRules.SpellbanCounter,
                Detail = "无实体：受到的伤害固定为 1",
                Source = ctx.Source,
            });
        }
    }

    /// <summary>暴击：来源的战斗伤害翻倍（延时保护之后→新层序=Amplify；圣盾/护甲吸收翻倍后的量）。</summary>
    public sealed class CritModifier : IDamageModifier
    {
        public int Stage => DamageStageOrder.Amplify;
        public int Priority => 20;

        public bool Applies(DamageContext ctx)
            => ctx.Request != null
               && ctx.Request.Kind == DamageKind.Combat
               && ctx.Source != null
               && ctx.Source.HasKeyword(KeywordRules.Crit)
               && CardEffectBudget.KeywordAllowed(ctx.Source); // §11 高危关键词：过载不触发

        public void Apply(DamageContext ctx)
        {
            ctx.Amount *= 2;
            CardEffectBudget.CommitKeyword(ctx.Source); // 结算后计（§11.3——第 30 次本身照常翻倍）
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ctx.Source,
                Keyword = KeywordRules.Crit,
                Detail = $"暴击：战斗伤害翻倍 → {ctx.Amount} 点",
                Source = ctx.Source,
            });
        }
    }

    /// <summary>易损（FinalModify）：剩余量 +层数放大、生效后全部移除（2026-11-09 双改版口径平移）。</summary>
    public sealed class VulnerableModifier : IDamageModifier
    {
        public int Stage => DamageStageOrder.FinalModify;
        public int Priority => 10;

        public bool Applies(DamageContext ctx)
            => ctx.Target.GetEffectiveCounterCount(CounterRules.VulnerableCounter) > 0;

        public void Apply(DamageContext ctx)
        {
            int vulnerable = ctx.Target.GetEffectiveCounterCount(CounterRules.VulnerableCounter);
            ctx.Amount += vulnerable;
            ctx.Target.AddCounters(CounterRules.VulnerableCounter, -vulnerable);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ctx.Target,
                Keyword = CounterRules.VulnerableCounter,
                Detail = $"易损放大 +{vulnerable} 点（生效后全部移除）",
                Source = ctx.Source,
            });
        }
    }

    // ============================== 500 Prevent ==============================

    public interface IPreventionLayer
    {
        int Priority { get; }
        int Prevent(DamageContext ctx, int amount); // 返回剩余量
    }

    public sealed class PreventStage : IDamageStage
    {
        private readonly List<IPreventionLayer> _layers;
        public int Order => DamageStageOrder.Prevent;

        public PreventStage(IEnumerable<IPreventionLayer> layers) { _layers = layers.ToList(); }

        public void Apply(DamageContext ctx)
        {
            int before = ctx.Amount;
            foreach (var layer in _layers.OrderBy(x => x.Priority))
            {
                if (ctx.Amount <= 0) break;
                ctx.Amount = layer.Prevent(ctx, ctx.Amount);
            }
            ctx.Prevented += before - ctx.Amount;
        }
    }

    /// <summary>免疫盾（NullShield）：归 0 提前出管线（无事件——Legacy ProtectionRules.ModifyDamage 同口径）。</summary>
    public sealed class NullShieldLayer : IPreventionLayer
    {
        public int Priority => 10;

        public int Prevent(DamageContext ctx, int amount)
            => ctx.Target.GetEffectiveCounterCount(CounterRules.NullShieldCounter) > 0 ? 0 : amount;
    }

    /// <summary>减半护盾（HalveShield）：ceil(amount/2)。层序注意：在暴击之后（§2.4 已裁决差异）。</summary>
    public sealed class HalveShieldLayer : IPreventionLayer
    {
        public int Priority => 20;

        public int Prevent(DamageContext ctx, int amount)
            => ctx.Target.GetEffectiveCounterCount(CounterRules.HalveShieldCounter) > 0 ? (amount + 1) / 2 : amount;
    }

    /// <summary>圣盾指示物：每层挡一次任意伤害、消耗 1 层（Legacy ApplyPreventionLayers 第 1 层平移）。</summary>
    public sealed class DivineShieldLayer : IPreventionLayer
    {
        public int Priority => 30;

        public int Prevent(DamageContext ctx, int amount)
        {
            int shieldLayers = ctx.Target.GetEffectiveCounterCount(CounterRules.DivineShieldCounter);
            if (shieldLayers <= 0) return amount;

            ctx.Target.AddCounters(CounterRules.DivineShieldCounter, -1);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ctx.Target,
                Keyword = CounterRules.DivineShieldCounter,
                Detail = $"圣盾抵挡 {amount} 点伤害（消耗 1 层，余 {shieldLayers - 1} 层）",
                Source = ctx.Source,
            });
            return 0;
        }
    }

    /// <summary>护甲指示物：逐点吸收（Entity 级——角色可持有；Legacy 第 2 层平移）。</summary>
    public sealed class ArmorLayer : IPreventionLayer
    {
        public int Priority => 40;

        public int Prevent(DamageContext ctx, int amount)
        {
            int armor = ctx.Target.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
            if (armor <= 0) return amount;

            int absorbed = Math.Min(armor, amount);
            ctx.Target.AddCounters(KeywordRules.ArmorCounter, -absorbed);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ctx.Target,
                Keyword = KeywordRules.ArmorCounter,
                Detail = $"护甲指示物吸收 {absorbed} 点",
                Source = ctx.Source,
            });
            return amount - absorbed;
        }
    }

    /// <summary>坚韧指示物：每层 −1、实际拦到即生效——生效后层数减半（Legacy 第 3 层平移）。</summary>
    public sealed class ToughnessLayer : IPreventionLayer
    {
        public int Priority => 50;

        public int Prevent(DamageContext ctx, int amount)
        {
            int toughness = ctx.Target.GetEffectiveCounterCount(CounterRules.ToughnessCounter);
            if (toughness <= 0) return amount;

            int remaining = Math.Max(0, amount - toughness);
            int toughHalved = toughness / 2;
            ctx.Target.AddCounters(CounterRules.ToughnessCounter, -(toughness - toughHalved));
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ctx.Target,
                Keyword = CounterRules.ToughnessCounter,
                Detail = $"坚韧减免 {toughness} 点（生效减半，余 {toughHalved} 层）",
                Source = ctx.Source,
            });
            return remaining;
        }
    }

    // ============================== 700 Apply ==============================

    public interface IDamageApplier
    {
        AppliedDamage Apply(DamageContext ctx);
    }

    public readonly struct AppliedDamage
    {
        public readonly int Dealt;
        public readonly bool Lethal;
        public AppliedDamage(int dealt, bool lethal) { Dealt = dealt; Lethal = lethal; }
    }

    /// <summary>落血层（Legacy 4 段平移）。致死标记与 Legacy 同款（pendingCause/Source + IsAlive=false，
    /// SBA checkpoint 消费）——草案 §2.2.5 的 PendingDeath 独立态留作后续精化：v1 链同步 checkpoint，
    /// 中途观察与 Legacy 一致，吸血门走 IsAlive 即等价。</summary>
    public sealed class ApplyStage : IDamageStage
    {
        private readonly IDamageApplier _applier;
        public int Order => DamageStageOrder.Apply;

        public ApplyStage(IDamageApplier applier) { _applier = applier; }

        public void Apply(DamageContext ctx)
        {
            if (ctx.Amount <= 0) { ctx.Dealt = 0; return; } // 全挡：不落血不产事件（Legacy 同口径）
            var applied = _applier.Apply(ctx);
            ctx.Dealt = applied.Dealt;
            ctx.Lethal = applied.Lethal;
        }
    }

    /// <summary>默认落血器：结界=耐久、单位=生命+生命锁+三轨制致死、角色=生命+生命锁（Legacy 逐行平移）。</summary>
    public sealed class DefaultDamageApplier : IDamageApplier
    {
        public AppliedDamage Apply(DamageContext ctx)
        {
            Entity source = ctx.Source;
            Entity target = ctx.Target;
            bool isCombat = ctx.Request != null && ctx.Request.Kind == DamageKind.Combat;

            int oldLife = (target as Player)?.Life ?? 0;
            ctx.OldLife = oldLife;

            int dealt = 0;
            bool lethal = false;

            if (target is Card card)
            {
                if (card.IsNonLivingUnit())
                {
                    int durBefore = card.GetCounterCount(CounterRules.DurabilityCounter);
                    CounterRules.LoseDurability(GameCore.Instance, card, ctx.Amount,
                        isCombat ? "战斗伤害" : "效果伤害");
                    dealt = Math.Max(0, durBefore - card.GetCounterCount(CounterRules.DurabilityCounter));
                }
                else
                {
                    int cardLifeBefore = card._life;
                    card._life -= ctx.Amount;
                    int cardLocked = ProtectionRules.ClampLife(card, card._life);
                    if (cardLocked != card._life)
                    {
                        card._life = cardLocked;
                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = card,
                            Keyword = "延时保护",
                            Detail = $"生命锁：{EffectText.Name(card)} 生命值锁定不低于 1",
                            Source = source,
                        });
                    }
                    dealt = Math.Max(0, cardLifeBefore - card._life);
                    int auraLife = GameBoard.LinkAuraSystem.GetLifeBonus(card);
                    if (card._life + auraLife <= 0)
                    {
                        card._life = 0;
                        card._pendingDeathCause = DeathCause.DamageLethal;
                        card._pendingDeathSource = source;
                        card.IsAlive = false; // 死亡决策/送墓交 SBA checkpoint（复生/落墓由 DeathRules 处理）
                        lethal = true;
                    }
                }
            }
            else if (target is Player player)
            {
                player.Life -= ctx.Amount;
                int locked = ProtectionRules.ClampLife(player, player.Life);
                if (locked != player.Life)
                {
                    player.Life = locked;
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = player,
                        Keyword = "延时保护",
                        Detail = $"生命锁：{EffectText.Name(player)} 生命值锁定不低于 1",
                        Source = source,
                    });
                }
                dealt = Math.Max(0, oldLife - player.Life);
                lethal = player.Life <= 0; // 角色死亡判定交 SBA ZeroLife（RoleDeath/终局流程）
            }

            return new AppliedDamage(dealt, lethal);
        }
    }

    // ============================== 默认装配 ==============================

    /// <summary>默认管线装配：各层内置适配器即 Legacy 段的逐行平移；新防御关键词=注册新层，不改管线。</summary>
    public static class DamagePipelineFactory
    {
        public static DamagePipeline CreateDefault()
        {
            var pipeline = new DamagePipeline(new DefaultTargetValidator());
            pipeline.Register(new ReplaceStage(new IDamageReplacement[] { new ReplacementEngineAdapter() }));
            pipeline.Register(new RedirectStage(new IDamageRedirect[] { new GuardianRedirect() }, new RedirectPolicy()));
            pipeline.Register(new AmplifyStage(new IDamageModifier[] { new SpellbanClampModifier(), new CritModifier() }));
            pipeline.Register(new PreventStage(new IPreventionLayer[]
            {
                new NullShieldLayer(), new HalveShieldLayer(), new DivineShieldLayer(), new ArmorLayer(), new ToughnessLayer(),
            }));
            pipeline.Register(new FinalModifyStage(new IDamageModifier[] { new VulnerableModifier() }));
            pipeline.Register(new ApplyStage(new DefaultDamageApplier()));
            return pipeline;
        }
    }
}
