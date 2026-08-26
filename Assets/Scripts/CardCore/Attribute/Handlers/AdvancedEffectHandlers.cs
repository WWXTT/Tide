using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 高级 handler — 栈操作 / 能力复制。
    // スタック保持側（StackEngine）を GameCore.Instance 経由で操作する。
    // ================================================================

    /// <summary>
    /// 重定向目标（栈顶 pending 效果の目标を本効果の解決 target へ差替）。
    /// UI 接続点：本来は重定向先をプレイヤーが選ぶ。現状は本原子効果の解決 target をそのまま採用。
    /// </summary>
    public class RedirectTargetHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RedirectTarget;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            ExecuteAsync(effect, context).Forget();
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var stack = GameCore.Instance?.StackEngine;
            if (stack == null) return;

            var topBefore = stack.Peek();
            var originalTarget = topBefore?.Targets != null && topBefore.Targets.Count > 0
                ? topBefore.Targets[0] : null;

            // 重定向先：从已解析的候选中由玩家选 1（候选≤1 时直接采用，AI/超时自动）。
            var candidates = context.Targets ?? new List<Entity>();
            List<Entity> chosen;
            if (candidates.Count <= 1)
            {
                chosen = candidates;
            }
            else
            {
                chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = context.Controller,
                    Title = "重定向目标",
                    Hint = "选择效果的新目标",
                });
            }

            if (chosen == null || chosen.Count == 0) return;
            if (!stack.RetargetTopStackObject(chosen)) return;

            PublishEvent(new RedirectTargetEvent
            {
                OriginalTarget = originalTarget,
                NewTarget = chosen[0],
                Source = context.Source,
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "重定向栈顶效果的目标";
    }

    /// <summary>
    /// 复制目标能力（PrimaryTarget(Card) の运行时效果を Source へコピー追加）。
    /// </summary>
    public class CopyTargetAbilityHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.CopyTargetAbility;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (!(context.PrimaryTarget is IHasRuntimeEffects targetEffects)) return;
            if (!(context.Source is IHasRuntimeEffects sourceEffects)) return;
            if (targetEffects.RuntimeEffects == null || targetEffects.RuntimeEffects.Count == 0) return;

            if (sourceEffects.RuntimeEffects == null)
                sourceEffects.RuntimeEffects = new List<IEffect>();

            sourceEffects.RuntimeEffects.AddRange(targetEffects.RuntimeEffects);

            if (context.PrimaryTarget is Card targetCard)
            {
                PublishEvent(new CardCopiedEvent
                {
                    OriginalCard = targetCard,
                    Controller = context.Controller,
                    Source = context.Source,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "复制目标的能力";
    }

    /// <summary>高级 handler ファクトリ</summary>
    public static class AdvancedEffectHandlerFactory
    {
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                new RedirectTargetHandler(),
                new CopyTargetAbilityHandler(),
            };
        }
    }
}
