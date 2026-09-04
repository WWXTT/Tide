using System;
using System.Collections;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 触发时机（单套命名定案，31 值连续；卡牌数据随本次收敛重建，int 重排定案）
    ///
    /// 双泳道定案：
    /// - 泳道A（不在场的卡·整卡使用）：手牌/隐蔽区 → 发动区三关（①双方可见宣言→②反制窗口→③未被反制结算代价）
    ///   → 栈(card cast，速度=记速器+1) → 结算 → 战场 → 登场时点(OnPlay)。
    /// - 泳道B（已在场的卡·效果发动）：不进发动区、不发使用宣言——激活式(Activate_*)走速度系统轮询，
    ///   触发式(On_Xxx)走事件匹配；场上效果发动的可观察时点 = 原子三阶段事件。
    /// </summary>
    public enum TriggerTiming
    {
        /// <summary>登场：经过发动区三关后进入战场之时（付费失败/被打落/被反制不进战场则不触发；横置非触发代价）</summary>
        OnPlay = 0,
        /// <summary>死亡时（亡语 / 进墓地时）</summary>
        OnDeath = 1,
        /// <summary>抽牌时</summary>
        OnDraw = 2,
        /// <summary>造成伤害时（payload 过滤：伤害源==自己）</summary>
        OnDealDamage = 3,
        /// <summary>受到伤害时（payload 过滤：受伤者==自己）</summary>
        OnTakeDamage = 4,
        /// <summary>回合开始时</summary>
        OnTurnStart = 5,
        /// <summary>回合结束时</summary>
        OnTurnEnd = 6,
        /// <summary>攻击宣言时（攻方）</summary>
        OnAttack = 7,
        /// <summary>被攻击时（被指方，payload 过滤：攻击目标==自己）</summary>
        OnAttacked = 8,
        /// <summary>进场时（任意来源含打出/效果召唤/复活/token/控制权变更——OnPlay 的超集）</summary>
        OnSummon = 9,
        /// <summary>超量素材取除时（暂无映射事件，待超量素材取除事件补齐）</summary>
        OnMaterialDetach = 10,
        /// <summary>被指定为目标时（原子效果开始作用且目标含自己）</summary>
        OnTargeted = 11,
        /// <summary>其他生物进入战场时（payload 过滤：进场的不是自己）</summary>
        OnOtherCreatureEnter = 12,
        /// <summary>施放法术时（使用宣言且打出的是法术）</summary>
        OnSpellCast = 13,
        /// <summary>横置时</summary>
        OnTap = 14,
        /// <summary>重置时</summary>
        OnUntap = 15,
        /// <summary>破坏时（"破坏"死因）</summary>
        OnDestroy = 16,
        /// <summary>除外时</summary>
        OnExile = 17,
        /// <summary>从墓地回到战场时（进场事件的 Revived 来源）</summary>
        OnReturnFromGraveyard = 18,

        // === 效果类型（非触发时机：激活式走速度系统轮询，不参与事件匹配）===
        /// <summary>激活式效果（主要阶段发动）</summary>
        Activate_Active = 19,
        /// <summary>瞬间发动</summary>
        Activate_Instant = 20,
        /// <summary>响应式发动</summary>
        Activate_Response = 21,

        // === 触发时机（续）===
        /// <summary>离场时（战场 → 任何非战场区）</summary>
        OnLeaveBattlefield = 22,
        /// <summary>阶段开始时</summary>
        OnPhaseStart = 23,
        /// <summary>阶段结束时</summary>
        OnPhaseEnd = 24,
        /// <summary>阻拦宣言时</summary>
        OnBlockDeclare = 25,
        /// <summary>使用卡牌时（使用宣言时点，付费前，含法术——与 OnPlay 登场分工）</summary>
        OnCardPlayed = 26,
        /// <summary>游戏开始时</summary>
        OnGameStart = 27,

        // === 原子效果三阶段事件（场上效果发动的可观察时点）===
        /// <summary>原子效果发动时</summary>
        OnAtomicEffectActivation = 28,
        /// <summary>原子效果开始作用时</summary>
        OnAtomicEffectStartApplying = 29,
        /// <summary>原子效果结算完成时</summary>
        OnAtomicEffectResolution = 30,
    }

    /// <summary>
    /// 触发效果 - 绑定触发时机+条件+效果
    /// </summary>
    [Serializable]
    public sealed class GeneratedTriggeredAbility
    {
        /// <summary>触发时机</summary>
        public TriggerTiming Timing { get; }

        /// <summary>是否可选触发</summary>
        public bool IsOptional { get; }

        /// <summary>每回合触发次数限制（0=无限）</summary>
        public int PerTurnLimit { get; }

        /// <summary>效果描述</summary>
        public string Description { get; }

        public GeneratedTriggeredAbility(
            TriggerTiming timing,
            bool isOptional = false,
            int perTurnLimit = 0,
            string description = "")
        {
            Timing = timing;
            IsOptional = isOptional;
            PerTurnLimit = perTurnLimit;
            Description = description;
        }
    }
}
