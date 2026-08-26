using System;


namespace CardCore
{
    /// <summary>
    /// 游戏事件基接口
    /// 所有游戏事件都实现此接口
    /// 继承 IEventData 以兼容统一事件管理器
    /// </summary>
    public interface IGameEvent : IEventData
    {
        /// <summary>
        /// 事件发生时间
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// 事件唯一标识
        /// </summary>
        uint EventId { get; }
    }

    /// <summary>
    /// 游戏事件基类
    /// </summary>
    public abstract class GameEventBase : IGameEvent
    {
        private static uint _nextEventId = 0;

        public DateTime Timestamp { get; } = DateTime.Now;
        public uint EventId { get; } = ++_nextEventId;
    }

    // ==================== 阶段事件 ====================

    /// <summary>
    /// 阶段开始事件
    /// </summary>
    public class PhaseStartEvent : GameEventBase
    {
        public PhaseType Phase { get; set; }
        public Player ActivePlayer { get; set; }
    }

    /// <summary>
    /// 阶段结束事件
    /// </summary>
    public class PhaseEndEvent : GameEventBase
    {
        public PhaseType Phase { get; set; }
        public Player ActivePlayer { get; set; }
    }

    /// <summary>
    /// 回合开始事件
    /// </summary>
    public class TurnStartEvent : GameEventBase
    {
        public Player TurnPlayer { get; set; }
        public int TurnNumber { get; set; }
    }

    /// <summary>
    /// 回合结束事件
    /// </summary>
    public class TurnEndEvent : GameEventBase
    {
        public Player TurnPlayer { get; set; }
        public int TurnNumber { get; set; }
    }

    // ==================== 卡牌相关事件 ====================

    /// <summary>
    /// 卡牌创建事件
    /// </summary>
    public class CardCreateEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Player Controller { get; set; }
    }

    /// <summary>
    /// 抽卡事件
    /// </summary>
    public class CardDrawEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card DrawnCard { get; set; }
        public int DrawCount { get; set; } // 一次抽多张
        public bool FirstDrawOfTurn { get; set; } // 本回合首次抽卡
    }

    /// <summary>
    /// 使用卡牌事件
    /// </summary>
    public class CardPlayEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card PlayedCard { get; set; }
        public PlayLocation Location { get; set; } // 场上/手牌使用
        public ManaType ChosenManaType { get; set; } // 多色卡选择
    }

    /// <summary>
    /// 卡牌移动事件
    /// </summary>
    public class CardMoveEvent : GameEventBase
    {
        public Card MovedCard { get; set; }
        public Zone From { get; set; }
        public Zone To { get; set; }
        public Player Controller { get; set; }
    }

    /// <summary>
    /// 区域变更事件
    /// </summary>
    public class CardZoneChangeEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Zone OldZone { get; set; }
        public Zone NewZone { get; set; }
        public Player Controller { get; set; }
    }

    /// <summary>
    /// 销毁事件
    /// </summary>
    public class CardDestroyEvent : GameEventBase
    {
        public Card DestroyedCard { get; set; }
        public DestroyReason Reason { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 流放事件
    /// </summary>
    public class CardBanishEvent : GameEventBase
    {
        public Card BanishedCard { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 入场事件
    /// </summary>
    public class CardPutToBattlefieldEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Player Controller { get; set; }
        public bool Tapped { get; set; } // 是否横置入场
    }

    /// <summary>
    /// 离场事件
    /// </summary>
    public class CardLeaveBattlefieldEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Player Controller { get; set; }
        public Zone Destination { get; set; }
    }

    // ==================== 战斗事件 ====================

    /// <summary>
    /// 战斗阶段开始事件
    /// </summary>
    public class CombatPhaseStartEvent : GameEventBase
    {
        public Player AttackingPlayer { get; set; }
        public Player DefendingPlayer { get; set; }
    }

    /// <summary>
    /// 战斗阶段结束事件
    /// </summary>
    public class CombatPhaseEndEvent : GameEventBase
    {
        public Player AttackingPlayer { get; set; }
        public Player DefendingPlayer { get; set; }
    }

    /// <summary>
    /// 攻击宣言事件
    /// </summary>
    public class AttackDeclarationEvent : GameEventBase
    {
        public Entity Attacker { get; set; }
        public Entity Target { get; set; }
        public Player AttackingPlayer { get; set; }
    }

    /// <summary>
    /// 阻拦宣言事件
    /// </summary>
    public class BlockDeclarationEvent : GameEventBase
    {
        public Entity Blocker { get; set; }
        public Entity Attacker { get; set; }
        public Player BlockingPlayer { get; set; }
    }

    /// <summary>
    /// 伤害事件
    /// </summary>
    public class DamageEvent : GameEventBase
    {
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public int Amount { get; set; }
    }

    /// <summary>
    /// 战斗伤害事件
    /// </summary>
    public class CombatDamageEvent : GameEventBase
    {
        public Entity Attacker { get; set; }
        public Entity Defender { get; set; }
        public int Damage { get; set; }
    }

    /// <summary>
    /// 生命值变更事件
    /// </summary>
    public class LifeChangeEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int OldLife { get; set; }
        public int NewLife { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 生命流失事件
    /// </summary>
    public class LifeLossEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 生命回复事件
    /// </summary>
    public class LifeGainEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 效果事件 ====================

    /// <summary>
    /// 效果发动事件
    /// </summary>
    public class EffectActivatedEvent : GameEventBase
    {
        public Effect ActivatedEffect { get; set; }
        public Player Activator { get; set; }
        public System.Collections.Generic.List<Entity> Targets { get; set; }
    }

    /// <summary>
    /// 效果结算事件
    /// </summary>
    public class EffectResolveEvent : GameEventBase
    {
        public Effect ResolvedEffect { get; set; }
        public EffectResolutionContext Context { get; set; }
    }

    /// <summary>
    /// 效果无效事件
    /// </summary>
    public class EffectNegatedEvent : GameEventBase
    {
        public Effect NegatedEffect { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 栈空事件
    /// </summary>
    public class StackEmptyEvent : GameEventBase
    {
        public Player LastPriorityHolder { get; set; }
    }

    // ==================== 状态事件 ====================

    /// <summary>
    /// 状态变更事件
    /// </summary>
    public class StateChangeEvent : GameEventBase
    {
        public StateChangeType Type { get; set; }
        public Entity Target { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }

    /// <summary>
    /// 横置事件
    /// </summary>
    public class TapEvent : GameEventBase
    {
        public Entity TappedEntity { get; set; }
        public bool IsUntapping { get; set; }
    }

    /// <summary>
    /// 横置恢复事件
    /// </summary>
    public class UntapEvent : GameEventBase
    {
        public Entity UntappedEntity { get; set; }
    }

    /// <summary>
    /// 控制权变更事件
    /// </summary>
    public class ControlChangeEvent : GameEventBase
    {
        public Entity ChangedEntity { get; set; }
        public Player OldController { get; set; }
        public Player NewController { get; set; }
        public Effect Source { get; set; }
    }

    // ==================== 替代事件 ====================

    /// <summary>
    /// 替代事件
    /// </summary>
    public class ReplacementEvent : GameEventBase
    {
        public IGameEvent OriginalEvent { get; set; }
        public Effect ReplacementEffect { get; set; }
        public System.Collections.Generic.List<ReplacementApplication> AppliedReplacements { get; set; }
    }

    // ==================== 游戏开始事件 ====================

    /// <summary>
    /// 游戏开始事件
    /// </summary>
    public class GameStartEvent : GameEventBase
    {
        public Player FirstPlayer { get; set; }
        public Player SecondPlayer { get; set; }
    }

    // ==================== 游戏结束事件 ====================

    /// <summary>
    /// 游戏结束事件
    /// </summary>
    public class GameOverEvent : GameEventBase
    {
        public Player Winner { get; set; }
        public Player Loser { get; set; }
        public GameOverReason Reason { get; set; }
        public int TotalTurns { get; set; }
    }

    // ==================== 触发事件 ====================

    /// <summary>
    /// 触发式能力事件
    /// </summary>
    public class TriggerEvent : GameEventBase
    {
        public TriggeredAbility TriggeredAbility { get; set; }
        public IGameEvent OriginalEvent { get; set; } // 触发此能力的事件
        public Entity TriggerSource { get; set; }
    }

    // ==================== 栈事件 ====================

    /// <summary>
    /// 栈添加事件
    /// </summary>
    public class StackAddEvent : GameEventBase
    {
        public IStackObject AddedObject { get; set; }
        public Player AddingPlayer { get; set; }
    }

    /// <summary>
    /// 栈结算开始事件
    /// </summary>
    public class StackResolutionStartEvent : GameEventBase
    {
        public int StackSize { get; set; }
    }

    /// <summary>
    /// 栈结算完成事件
    /// </summary>
    public class StackResolutionEndEvent : GameEventBase
    {
        public int StackSize { get; set; }
    }

    /// <summary>
    /// 优先权传递事件
    /// </summary>
    public class PriorityPassEvent : GameEventBase
    {
        public Player PassingPlayer { get; set; }
        public bool BothPassed { get; set; } // 双方连续Pass
    }

    /// <summary>
    /// 优先权获得事件
    /// </summary>
    public class PriorityGainEvent : GameEventBase
    {
        public Player GainingPlayer { get; set; }
    }

    // ==================== 牌库操作事件 ====================

    /// <summary>
    /// 牌库检索事件
    /// </summary>
    public class CardSearchEvent : GameEventBase
    {
        public Player Player { get; set; }
        public System.Collections.Generic.List<Card> SearchedCards { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 洗牌事件
    /// </summary>
    public class CardShuffleEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 关键词事件 ====================

    /// <summary>
    /// 关键词变更事件
    /// </summary>
    public class KeywordChangeEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public string KeywordId { get; set; }
        public bool Added { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 效果反制事件 ====================

    /// <summary>
    /// 效果被反制事件
    /// </summary>
    public class EffectCounteredEvent : GameEventBase
    {
        public EffectInstance CounteredEffect { get; set; }
        public Entity Source { get; set; }
        public Player Controller { get; set; }
    }

    // ==================== 衍生物事件 ====================

    /// <summary>
    /// 衍生物创建事件
    /// </summary>
    public class TokenCreatedEvent : GameEventBase
    {
        public string TokenTemplateId { get; set; }
        public Player Controller { get; set; }
        public Entity Source { get; set; }
        public bool Tapped { get; set; }
    }

    // ==================== 卡牌复制/转化事件 ====================

    /// <summary>
    /// 卡牌复制事件
    /// </summary>
    public class CardCopiedEvent : GameEventBase
    {
        public Card OriginalCard { get; set; }
        public Player Controller { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 属性交换事件
    /// </summary>
    public class StatsSwappedEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 卡牌无效化事件
    /// </summary>
    public class CardNullifiedEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public NullifyType NullifyType { get; set; }
        public Entity Source { get; set; }
    }
}
