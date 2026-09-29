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
        /// 获取效果描述（2026-09-16 接口化定案：唯一描述口，模板 + 上下文合成）。
        /// 栈执行时传入执行上下文（含解析后目标与 LastOutcome 真实产出）→ 完整执行文本；
        /// 无执行场景（代价文本等）传 null → 纯模板。
        /// </summary>
        /// <param name="effect">原子效果实例</param>
        /// <param name="context">执行上下文（可为 null）</param>
        string GetDescription(AtomicEffectInstance effect, EffectExecutionContext context);

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
                // 无目标原子（域空）与无目标效果照常执行；其余缺目标视为无效
                if (effect.TargetKinds == null || effect.TargetKinds.Count == 0)
                    return true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 执行期完整描述（模板方法，2026-09-16 接口化定案）：DescribeTemplate 模板主干 +
        /// 上下文合成（目标名 + LastOutcome 真实产出——handler 结算时写入，描述不重掷随机）。
        /// context=null（代价文本等无执行场景）退纯模板。
        /// </summary>
        public string GetDescription(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect == null) return "";
            var text = DescribeTemplate(effect);
            if (context == null) return text;

            if (context.Targets != null && context.Targets.Count > 0)
                text += $" → {EffectText.DescribeEntities(context.Targets)}";
            var outcome = EffectText.DescribeOutcome(context.LastOutcome);
            if (outcome.Length > 0)
                text += $"（{outcome}）";
            return text;
        }

        /// <summary>
        /// 模板主干（原 70+ 处 GetDescription 覆写层的归宿）：句式由各 handler 覆写提供。
        /// 默认回落原子表行 DisplayName（原 GetPreview 取数口径），再回落枚举名。
        /// </summary>
        protected virtual string DescribeTemplate(AtomicEffectInstance effect)
        {
            var config = GetConfig(effect.Type);
            return config?.DisplayName ?? effect.Type.ToString();
        }

        /// <summary>
        /// 获取效果配置
        /// </summary>
        protected AtomicEffectConfig GetConfig(AtomicEffectType type)
        {
            return AtomicEffectTable.GetByType(type);
        }

        /// <summary>
        /// 发布游戏事件——经 GameCore 统一路由（替代检查 → 总线 → Trigger/Layer 引擎）。
        /// 修复：原直发总线使触发式收不到 handler 事件（抽卡/伤害/死亡…时点全哑）。
        /// </summary>
        protected void PublishEvent<T>(T gameEvent) where T : IGameEvent
        {
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(gameEvent);
            else EventManager.Instance.Publish(gameEvent);
        }
    }
}
