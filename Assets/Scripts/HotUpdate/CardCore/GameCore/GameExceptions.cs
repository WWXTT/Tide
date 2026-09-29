using System;

namespace CardCore
{
    /// <summary>
    /// 游戏规则违反异常
    /// 当操作不符合游戏规则时抛出（如非法召唤、无效行动等）
    /// </summary>
    public class GameRuleViolationException : Exception
    {
        public string RuleId { get; }

        public GameRuleViolationException(string message, string ruleId = null)
            : base(message)
        {
            RuleId = ruleId;
        }

        public GameRuleViolationException(string message, Exception inner, string ruleId = null)
            : base(message, inner)
        {
            RuleId = ruleId;
        }
    }

    /// <summary>
    /// 无效目标异常
    /// 当效果指定了非法或无效的目标时抛出
    /// </summary>
    public class InvalidTargetException : GameRuleViolationException
    {
        public Entity InvalidTarget { get; }

        public InvalidTargetException(Entity target, string message = null)
            : base(message ?? $"Invalid target: {target}", "INVALID_TARGET")
        {
            InvalidTarget = target;
        }
    }

    /// <summary>
    /// 效果解析异常
    /// 当效果执行过程中发生错误时抛出
    /// </summary>
    public class EffectResolutionException : Exception
    {
        public Effect SourceEffect { get; }
        public EffectInstance EffectInstance { get; }

        public EffectResolutionException(Effect sourceEffect, string message)
            : base(message)
        {
            SourceEffect = sourceEffect;
        }

        public EffectResolutionException(EffectInstance instance, string message, Exception inner = null)
            : base(message, inner)
        {
            EffectInstance = instance;
            SourceEffect = instance?.SourceEffect;
        }
    }

    /// <summary>
    /// 非法操作异常
    /// 当在错误的游戏状态或时机下尝试操作时抛出
    /// </summary>
    public class IllegalOperationException : GameRuleViolationException
    {
        public GameState CurrentState { get; }
        public string Operation { get; }

        public IllegalOperationException(GameState currentState, string operation, string message = null)
            : base(message ?? $"Cannot perform '{operation}' in state {currentState}", "ILLEGAL_OPERATION")
        {
            CurrentState = currentState;
            Operation = operation;
        }
    }

    /// <summary>
    /// 区域操作异常
    /// 当卡牌区域操作不合法时抛出
    /// </summary>
    public class ZoneOperationException : GameRuleViolationException
    {
        public Card Card { get; }
        public Zone FromZone { get; }
        public Zone ToZone { get; }

        public ZoneOperationException(Card card, Zone from, Zone to, string message = null)
            : base(message ?? $"Invalid zone operation: {card?.ID} from {from} to {to}", "INVALID_ZONE_OP")
        {
            Card = card;
            FromZone = from;
            ToZone = to;
        }
    }
}
