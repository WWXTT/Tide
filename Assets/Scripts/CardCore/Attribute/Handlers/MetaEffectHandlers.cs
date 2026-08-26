using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 元/合成効果 handler — 设计「原子効果を节点连接で组装／条件分支节点」对应的合成原语。
    // 子効果は AtomicEffectInstance.SubEffects に保持し、RunSub で個別解決する。
    // ================================================================

    /// <summary>メタ効果の共有ヘルパ</summary>
    internal static class MetaEffectHelpers
    {
        /// <summary>
        /// 子効果を実行する。親 context から Source/Controller/ZoneManager/ElementPool/TriggeringEvent を
        /// 引継ぎ、Targets はリセット（registry 側で config に応じて自動解決させる）。
        /// 交互子効果（ChooseOne 等）が await UI できるよう async。
        /// </summary>
        internal static UniTask RunSubAsync(AtomicEffectInstance child, EffectExecutionContext parent)
        {
            if (child == null || parent == null) return UniTask.CompletedTask;

            var ctx = new EffectExecutionContext
            {
                Source = parent.Source,
                Controller = parent.Controller,
                Targets = new List<Entity>(),
                TriggeringEvent = parent.TriggeringEvent,
                ZoneManager = parent.ZoneManager,
                ElementPool = parent.ElementPool,
            };

            return EffectHandlerRegistry.ExecuteEffectAsync(child, ctx);
        }
    }

    /// <summary>重复效果（SubEffects を Value 回 順次実行）</summary>
    public class RepeatEffectHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RepeatEffect;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            ExecuteAsync(effect, context).Forget();
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect.SubEffects == null || effect.SubEffects.Count == 0) return;

            int times = context.GetValueAfterModifiers(effect.Value);
            if (times <= 0) times = 1;

            for (int i = 0; i < times; i++)
                foreach (var child in effect.SubEffects)
                    await MetaEffectHelpers.RunSubAsync(child, context);
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"重复执行 {effect.Value} 次";
    }

    /// <summary>随机效果（SubEffects から無作為 1 件を実行）</summary>
    public class RandomEffectHandler : AtomicEffectHandlerBase
    {
        private static readonly Random _rng = new Random();

        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RandomEffect;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            ExecuteAsync(effect, context).Forget();
        }

        public override UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect.SubEffects == null || effect.SubEffects.Count == 0) return UniTask.CompletedTask;

            int idx = _rng.Next(effect.SubEffects.Count);
            return MetaEffectHelpers.RunSubAsync(effect.SubEffects[idx], context);
        }

        public override string GetDescription(AtomicEffectInstance effect) => "随机执行一个效果";
    }

    /// <summary>
    /// 选其一效果（ChoiceRequestEvent 发行 → 选定 index の SubEffect を実行）。
    /// UI 接続点：当前以 effect.Value（既定 0）を暂定 index とし、UI 接続後に SelectedIndex を採用する。
    /// </summary>
    public class ChooseOneEffectHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ChooseOneEffect;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            ExecuteAsync(effect, context).Forget();
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect.SubEffects == null || effect.SubEffects.Count == 0) return;

            var options = effect.SubEffects.Select(e => e.GetDescription()).ToList();

            // 通知用に従来の ChoiceRequestEvent も発行継続（既存購読者向け）
            PublishEvent(new ChoiceRequestEvent
            {
                Chooser = context.Controller,
                Options = options,
                SelectedIndex = effect.Value,
                Source = context.Source,
            });

            // 目标选择器で玩家に 1 of N を選ばせる（AI/ヘッドレス/タイムアウトは自動）。
            int idx = await TargetSelectionService.RequestOneIndexAsync(
                context.Controller, options, "选择一个效果");
            if (idx < 0 || idx >= effect.SubEffects.Count) idx = 0;

            await MetaEffectHelpers.RunSubAsync(effect.SubEffects[idx], context);
        }

        public override string GetDescription(AtomicEffectInstance effect) => "选择其中一个效果";
    }

    /// <summary>
    /// 延迟效果（SubEffects と発火タイミングを DelayedEffectScheduler に登録）。
    /// タイミング：effect.StringValue（既定 "TurnEnd"）。"PhaseEnd" 指定で相位终了に発火。
    /// </summary>
    public class DelayedEffectHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DelayedEffect;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect.SubEffects == null || effect.SubEffects.Count == 0) return;

            var scheduler = GameCore.Instance?.DelayedEffectScheduler;
            if (scheduler == null) return;

            string timing = string.IsNullOrEmpty(effect.StringValue) ? "TurnEnd" : effect.StringValue;
            scheduler.Schedule(context.Controller, context.Source, effect.SubEffects, timing,
                context.TriggeringEvent, context.ZoneManager, context.ElementPool);

            PublishEvent(new DelayedEffectScheduledEvent
            {
                Controller = context.Controller,
                FireTiming = timing,
                Source = context.Source,
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "延迟触发效果";
    }

    /// <summary>元/合成効果 handler ファクトリ</summary>
    public static class MetaEffectHandlerFactory
    {
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                new RepeatEffectHandler(),
                new RandomEffectHandler(),
                new ChooseOneEffectHandler(),
                new DelayedEffectHandler(),
            };
        }
    }
}
