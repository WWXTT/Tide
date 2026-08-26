using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 触发频率
    /// </summary>
    public enum TriggerFrequency
    {
        /// <summary>无限制</summary>
        Unlimited,
        /// <summary>每回合一次</summary>
        OncePerTurn,
        /// <summary>每局一次</summary>
        OncePerGame
    }

    /// <summary>
    /// 触发可控性
    /// </summary>
    public enum TriggerControllability
    {
        /// <summary>玩家完全可控</summary>
        PlayerControlled,
        /// <summary>部分可控</summary>
        PartialControl,
        /// <summary>不可控</summary>
        Uncontrolled
    }

    /// <summary>
    /// 触发价��配置（运行时）
    /// </summary>
    public class TriggerValueRuntimeConfig
    {
        // 触发难度系数
        public float EasyTrigger { get; set; } = 0.8f;
        public float NormalTrigger { get; set; } = 0.6f;
        public float HardTrigger { get; set; } = 0.4f;
        public float RareTrigger { get; set; } = 0.2f;

        // 触发频率系数
        public float OncePerTurnMultiplier { get; set; } = 0.9f;
        public float OncePerGameMultiplier { get; set; } = 0.7f;
        public float UnlimitedMultiplier { get; set; } = 1.0f;

        // 可控性系数
        public float PlayerControlledMultiplier { get; set; } = 1.0f;
        public float PartialControlMultiplier { get; set; } = 0.85f;
        public float UncontrolledMultiplier { get; set; } = 0.7f;

        /// <summary>
        /// 根据触发时点确定可控性
        /// </summary>
        public static TriggerControllability DetermineControllability(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Active or
                TriggerTiming.Activate_Instant or
                TriggerTiming.On_TurnStart or
                TriggerTiming.On_TurnEnd or
                TriggerTiming.On_PhaseStart or
                TriggerTiming.On_PhaseEnd or
                TriggerTiming.On_AttackDeclare => TriggerControllability.PlayerControlled,

                TriggerTiming.On_EnterBattlefield or
                TriggerTiming.On_LeaveBattlefield or
                TriggerTiming.On_Death or
                TriggerTiming.On_DamageDealt or
                TriggerTiming.On_DamageTaken or
                TriggerTiming.On_CardDraw or
                TriggerTiming.On_CardPlay  => TriggerControllability.PartialControl,

                _ => TriggerControllability.Uncontrolled
            };
        }

        /// <summary>
        /// 获取触发价值
        /// </summary>
        public float GetTriggerValue(TriggerTiming timing, TriggerFrequency frequency)
        {
            float baseValue = timing switch
            {
                TriggerTiming.Activate_Active or TriggerTiming.Activate_Instant => 1.0f,
                TriggerTiming.On_EnterBattlefield => EasyTrigger,
                TriggerTiming.On_TurnStart or TriggerTiming.On_TurnEnd => EasyTrigger,
                TriggerTiming.On_PhaseStart or TriggerTiming.On_PhaseEnd => NormalTrigger,
                _ => NormalTrigger
            };

            float frequencyMultiplier = frequency switch
            {
                TriggerFrequency.OncePerTurn => OncePerTurnMultiplier,
                TriggerFrequency.OncePerGame => OncePerGameMultiplier,
                _ => UnlimitedMultiplier
            };

            var controllability = DetermineControllability(timing);
            float controllabilityMultiplier = controllability switch
            {
                TriggerControllability.PlayerControlled => PlayerControlledMultiplier,
                TriggerControllability.PartialControl => PartialControlMultiplier,
                _ => UncontrolledMultiplier
            };

            return baseValue * frequencyMultiplier * controllabilityMultiplier;
        }

        public static TriggerValueRuntimeConfig CreateDefault()
        {
            return new TriggerValueRuntimeConfig();
        }
    }
}
