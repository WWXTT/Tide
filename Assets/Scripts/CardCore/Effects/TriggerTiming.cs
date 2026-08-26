using System;
using System.Collections;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 触发时机
    /// </summary>
    public enum TriggerTiming
    {
        /// <summary>打出时（战吼 / 进场时）</summary>
        OnPlay = 0,
        /// <summary>死亡时（亡语 / 进墓地时）</summary>
        OnDeath = 1,
        /// <summary>抽牌时</summary>
        OnDraw = 2,
        /// <summary>造成伤害时</summary>
        OnDealDamage = 3,
        /// <summary>受到伤害时</summary>
        OnTakeDamage = 4,
        /// <summary>回合开始时</summary>
        OnTurnStart = 5,
        /// <summary>回合结束时</summary>
        OnTurnEnd = 6,
        /// <summary>攻击时</summary>
        OnAttack = 7,
        /// <summary>被攻击时</summary>
        OnAttacked = 8,
        /// <summary>召唤时（YGO特有：生物进入战场）</summary>
        OnSummon = 9,
        /// <summary>超量素材取除时</summary>
        OnMaterialDetach = 10,
        /// <summary>被指定为目标时</summary>
        OnTargeted = 11,
        /// <summary>其他生物进入战场时</summary>
        OnOtherCreatureEnter = 12,
        /// <summary>施放法术时</summary>
        OnSpellCast = 13,
        /// <summary>横置时</summary>
        OnTap = 14,
        /// <summary>重置时</summary>
        OnUntap = 15,
        /// <summary>破坏时（YGO中的"破坏"）</summary>
        OnDestroy = 16,
        /// <summary>除外时</summary>
        OnExile = 17,
        /// <summary>从墓地回到战场时</summary>
        OnReturnFromGraveyard = 18,

        // === 效果类型（非触发时机）===
        /// <summary>激活式效果（主要阶段发动）</summary>
        Activate_Active = 19,
        /// <summary>瞬间发动</summary>
        Activate_Instant = 20,
        /// <summary>响应式发动</summary>
        Activate_Response = 21,

        // === 触发时机（下划线命名兼容）===
        /// <summary>入场时</summary>
        On_EnterBattlefield = 22,
        /// <summary>离场时</summary>
        On_LeaveBattlefield = 23,
        /// <summary>死亡时</summary>
        On_Death = 24,
        /// <summary>回合开始时</summary>
        On_TurnStart = 25,
        /// <summary>回合结束时</summary>
        On_TurnEnd = 26,
        /// <summary>阶段开始时</summary>
        On_PhaseStart = 27,
        /// <summary>阶段结束时</summary>
        On_PhaseEnd = 28,
        /// <summary>攻击宣言时</summary>
        On_AttackDeclare = 29,
        /// <summary>阻拦宣言时</summary>
        On_BlockDeclare = 30,
        /// <summary>造成伤害时</summary>
        On_DamageDealt = 31,
        /// <summary>受到伤害时</summary>
        On_DamageTaken = 32,
        /// <summary>抽卡时</summary>
        On_CardDraw = 33,
        /// <summary>使用卡牌时</summary>
        On_CardPlay = 34,
        /// <summary>游戏开始时</summary>
        On_GameStart = 35,

        // === 原子效果三阶段事件 ===
        /// <summary>原子效果发动时</summary>
        On_AtomicEffectActivation = 36,
        /// <summary>原子效果开始作用时</summary>
        On_AtomicEffectStartApplying = 37,
        /// <summary>原子效果结算完成时</summary>
        On_AtomicEffectResolution = 38,
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
