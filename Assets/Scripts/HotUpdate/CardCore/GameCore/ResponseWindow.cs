using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 响应窗口候选（发动弹窗条目，2026-09-16 战斗接入栈机器定案）：
    /// 速度系统收集的可发主动效果——守卫能力（速度1响应）/待发自愿效果/可打手牌卡/启动式能力。
    /// 收集时**双过滤**：元素费用（CanAfford 含 pending 承诺）+ 特殊代价（CostHandlerRegistry），
    /// 付不起的不进弹窗；**候选=0 跳过弹窗直接 Pass**。
    /// </summary>
    public class ResponseOption
    {
        public enum ResponseKind { GuardAbility, VoluntaryEffect, HandCard, ActivatedAbility }

        public ResponseKind Kind;
        /// <summary>弹窗显示文本（名称+费用摘要）。</summary>
        public string Label;

        /// <summary>守卫者/能力宿主/手牌卡。</summary>
        public Card SourceCard;
        /// <summary>待发自愿效果（Kind=VoluntaryEffect）。</summary>
        public PendingEffect Pending;
        /// <summary>启动式定义（Kind=ActivatedAbility）。</summary>
        public EffectDefinition Definition;
        /// <summary>拦截目标（Kind=GuardAbility：栈上的攻击宣言对象）。</summary>
        public EffectInstance AttackInstance;

        /// <summary>费用摘要（收集时已过可付性检查，此为显示用）。</summary>
        public string CostSummary;
    }

    /// <summary>
    /// 响应窗口服务（2026-09-16）：人类弹窗处理器与 AI 决策器的注册点。
    /// UI 注册 HumanResponder（ShowChoiceOverlay 简版弹窗）；AI 侧注册/内置启发决策；
    /// 均未注册（无头/训练）→ 候选自动放弃（等效旧行为：双 Pass 排干）。
    /// </summary>
    public static class ResponseWindowService
    {
        /// <summary>人类响应弹窗处理器：返回所选候选，null=跳过（Pass）。
        /// 弹窗由 UI 层负责（筛选 0 时收集口直接 Pass，不会调用本处理器）。</summary>
        public static Func<Player, List<ResponseOption>, UniTask<ResponseOption>> HumanResponder;

        /// <summary>AI 响应决策器：返回所选候选或 null（Pass）。缺省内置守卫启发（SimpleAI 同款）。</summary>
        public static Func<GameCore, Player, List<ResponseOption>, ResponseOption> AiResponder;
    }
}
