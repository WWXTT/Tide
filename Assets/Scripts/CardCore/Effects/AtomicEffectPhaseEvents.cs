using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 原子效果生命周期阶段
    /// 每个原子效果从发动到结算经历三个阶段
    /// </summary>
    public enum AtomicEffectPhase
    {
        /// <summary>发动阶段 - 效果被激活时</summary>
        Activation,
        /// <summary>开始作用阶段 - 效果开始对目标产生影响时</summary>
        StartApplying,
        /// <summary>结算完成阶段 - 效果执行完毕时</summary>
        ResolutionComplete
    }

    /// <summary>
    /// 原子效果阶段事件
    /// 统一的事件类，通过 EffectType + Phase 描述任意原子效果的三阶段
    /// TriggerEngine 可通过此事件匹配 On_AtomicEffectActivation / StartApplying / Resolution 触发时机
    /// </summary>
    public class AtomicEffectPhaseEvent : GameEventBase
    {
        /// <summary>触发的原子效果类型</summary>
        public AtomicEffectType EffectType { get; set; }

        /// <summary>当前所处阶段</summary>
        public AtomicEffectPhase Phase { get; set; }

        /// <summary>效果来源实体</summary>
        public Entity Source { get; set; }

        /// <summary>效果目标列表</summary>
        public List<Entity> Targets { get; set; }

        /// <summary>触发此事件的原子效果实例</summary>
        public AtomicEffectInstance EffectInstance { get; set; }

        /// <summary>效果执行上下文</summary>
        public EffectExecutionContext Context { get; set; }

        /// <summary>
        /// 将阶段映射到 TriggerTiming 枚举
        /// </summary>
        public TriggerTiming ToTriggerTiming()
        {
            return Phase switch
            {
                AtomicEffectPhase.Activation => TriggerTiming.On_AtomicEffectActivation,
                AtomicEffectPhase.StartApplying => TriggerTiming.On_AtomicEffectStartApplying,
                AtomicEffectPhase.ResolutionComplete => TriggerTiming.On_AtomicEffectResolution,
                _ => TriggerTiming.OnPlay
            };
        }
    }
}
