using System;
using Cysharp.Threading.Tasks;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果处理器接口
    /// 每种原子效果都需要实现此接口来定义具体的执行逻辑
    /// </summary>
    public interface IAtomicEffectHandler
    {
        /// <summary>
        /// 处理的效果类型
        /// </summary>
        AtomicEffectType EffectType { get; }

        /// <summary>
        /// 执行效果
        /// </summary>
        /// <param name="effect">原子效果实例</param>
        /// <param name="context">执行上下文</param>
        void Execute(AtomicEffectInstance effect, EffectExecutionContext context);

        /// <summary>
        /// 异步执行效果（支持 UI 等待，如目标选择弹窗）。
        /// 非交互 handler 由基类默认实现桥接到同步 Execute；交互 handler 需 override。
        /// </summary>
        /// <param name="effect">原子效果实例</param>
        /// <param name="context">执行上下文</param>
        UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context);

        /// <summary>
        /// 检查效果是否可以执行
        /// </summary>
        /// <param name="effect">原子效果实例</param>
        /// <param name="context">执行上下文</param>
        /// <returns>是否可以执行</returns>
        bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context);

        /// <summary>
        /// 获取效果描述
        /// </summary>
        /// <param name="effect">原子效果实例</param>
        /// <returns>描述文本</returns>
        string GetDescription(AtomicEffectInstance effect);

        /// <summary>
        /// 获取效果预览（用于UI显示）
        /// </summary>
        /// <param name="effect">原子效果实例</param>
        /// <param name="context">执行上下文（可能为null）</param>
        /// <returns>预览信息</returns>
        EffectPreviewInfo GetPreview(AtomicEffectInstance effect, EffectExecutionContext context);
    }

    /// <summary>
    /// 效果预览信息
    /// 用于UI显示效果执行前的预览
    /// </summary>
    [Serializable]
    public struct EffectPreviewInfo
    {
        /// <summary>效果描述文本</summary>
        public string Description;

        /// <summary>预期影响的实体数量</summary>
        public int TargetCount;

        /// <summary>预期数值（伤害/治疗量等）</summary>
        public int ExpectedValue;

        /// <summary>是否有风险（可能产生负面效果）</summary>
        public bool HasRisk;

        /// <summary>效果图标路径</summary>
        public string IconPath;

        public static EffectPreviewInfo Empty => new EffectPreviewInfo
        {
            Description = "",
            TargetCount = 0,
            ExpectedValue = 0,
            HasRisk = false,
            IconPath = "",
        };
    }

    /// <summary>
    /// 原子效果处理器基类
    /// 提供通用的辅助方法
    /// </summary>
    public abstract class AtomicEffectHandlerBase : IAtomicEffectHandler
    {
        /// <summary>
        /// 覆盖的效果类型（用于让同一个handler类处理不同的效果类型）
        /// </summary>
        public AtomicEffectType? OverrideEffectType { get; set; } = null;

        /// <summary>
        /// 抽象效果类型（子类必须实现，但可通过OverrideEffectType覆盖）
        /// </summary>
        protected abstract AtomicEffectType DefaultEffectType { get; }

        public AtomicEffectType EffectType => OverrideEffectType ?? DefaultEffectType;

        public abstract void Execute(AtomicEffectInstance effect, EffectExecutionContext context);

        /// <summary>
        /// 默认实现：桥接到同步 Execute 并立即完成。
        /// 需要等待 UI（如目标选择）的 handler 应 override 本方法。
        /// </summary>
        public virtual UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            Execute(effect, context);
            return UniTask.CompletedTask;
        }

        public virtual bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 默认实现：检查目标是否有效
            if (context == null) return false;
            if (context.Targets == null || context.Targets.Count == 0)
            {
                // 某些效果不需要目标
                var config = AtomicEffectTable.GetByType(effect.Type);
                if (config != null && config.TargetType == EffectTargetType.None)
                    return true;
                if (config != null && config.TargetType == EffectTargetType.Self)
                    return true;
                return false;
            }
            return true;
        }

        public virtual string GetDescription(AtomicEffectInstance effect)
        {
            return effect.GetDescription();
        }

        public virtual EffectPreviewInfo GetPreview(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var config = AtomicEffectTable.GetByType(effect.Type);
            if (config == null) return EffectPreviewInfo.Empty;

            return new EffectPreviewInfo
            {
                Description = GetDescription(effect),
                TargetCount = context?.Targets?.Count ?? 0,
                ExpectedValue = effect.Value,
                IconPath = GetIconPath(effect),
            };
        }

        /// <summary>
        /// 获取效果图标路径
        /// </summary>
        protected virtual string GetIconPath(AtomicEffectInstance effect)
        {
            return $"Icons/Effects/{effect.Type}";
        }

        /// <summary>
        /// 获取效果配置
        /// </summary>
        protected AtomicEffectConfig GetConfig(AtomicEffectType type)
        {
            return AtomicEffectTable.GetByType(type);
        }

        /// <summary>
        /// 发布游戏事件
        /// </summary>
        protected void PublishEvent<T>(T gameEvent) where T : IGameEvent
        {
            EventManager.Instance.Publish(gameEvent);
        }
    }
}
