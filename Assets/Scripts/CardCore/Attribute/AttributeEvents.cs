using System;
using System.Collections.Generic;
using GameEventBase = CardCore.GameEventBase;  // 别名引用父命名空间的类

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果处理器的游戏事件定义
    /// 所有事件继承自 GameEventBase 以正确实现 IGameEvent
    /// </summary>

    // ==================== 伤害类事件 ====================

    /// <summary>原子伤害事件（含伤害类型信息）</summary>
    public class AtomicDamageEvent : GameEventBase
    {
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public int Damage { get; set; }
        public bool IsCombatDamage { get; set; }
        public DamageType DamageType { get; set; }
    }

    /// <summary>治疗事件</summary>
    public class HealEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 卡牌操作事件 ====================

    /// <summary>弃牌事件</summary>
    public class CardDiscardEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card Card { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>磨牌事件</summary>
    public class CardMillEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card Card { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>返回手牌事件</summary>
    public class CardReturnToHandEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>流放事件（处理器版，含来源区域）</summary>
    public class CardExileEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Entity Source { get; set; }
        public Zone FromZone { get; set; }
    }

    /// <summary>洗入牌库事件</summary>
    public class CardShuffleIntoDeckEvent : GameEventBase
    {
        public Card Card { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>疲劳事件</summary>
    public class FatigueEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int Damage { get; set; }
    }

    /// <summary>展示手牌事件</summary>
    public class RevealHandEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Cards { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 状态变更事件 ====================

    /// <summary>属性修改事件</summary>
    public class StatModifyEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public StatType StatType { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
        public int Delta { get; set; }
        public DurationType Duration { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>属性设置事件</summary>
    public class StatSetEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public StatType StatType { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>属性交换事件</summary>
    public class SwapStatsEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public int OldPower { get; set; }
        public int NewPower { get; set; }
        public int OldLife { get; set; }
        public int NewLife { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>指示物事件</summary>
    public class CounterEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public string CounterType { get; set; }
        public int Amount { get; set; }
        public bool IsAdd { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>关键词事件</summary>
    public class KeywordEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public string Keyword { get; set; }
        public bool IsAdd { get; set; }
        public DurationType Duration { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>冻结事件</summary>
    public class FreezeEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public DurationType Duration { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>费用修改事件</summary>
    public class CostModifyEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public int OldCost { get; set; }
        public int NewCost { get; set; }
        public int Delta { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 控制类事件 ====================

    /// <summary>控制权变更事件（原子效果版）</summary>
    public class AttrControlChangeEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public Player OldController { get; set; }
        public Player NewController { get; set; }
        public bool IsPermanent { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>交换控制者事件</summary>
    public class SwapControllerEvent : GameEventBase
    {
        public Entity Target1 { get; set; }
        public Entity Target2 { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>伤害防止事件</summary>
    public class PreventDamageEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public int Amount { get; set; }
        public DurationType Duration { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>反制法术事件</summary>
    public class CounterSpellEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>无效化事件</summary>
    public class NegateEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public bool IsActivation { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>重定向目标事件</summary>
    public class RedirectTargetEvent : GameEventBase
    {
        public Entity OriginalTarget { get; set; }
        public Entity NewTarget { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>无效化卡牌事件</summary>
    public class NullifyEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>占卜事件</summary>
    public class ScryEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Cards { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>额外回合事件</summary>
    public class ExtraTurnEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>跳过回合事件</summary>
    public class SkipTurnEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>展示卡牌事件</summary>
    public class RevealCardsEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Cards { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 选择请求事件（ChooseOneEffect 发行）。
    /// UI 层订阅后向玩家呈现 Options，并写回选择 index；逻辑层当前以暂定 index 直接确定。
    /// </summary>
    public class ChoiceRequestEvent : GameEventBase
    {
        public Player Chooser { get; set; }
        public List<string> Options { get; set; }
        public int SelectedIndex { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>延迟效果登记事件（DelayedEffect 发行，供 UI/日志感知一个延迟效果已挂起）</summary>
    public class DelayedEffectScheduledEvent : GameEventBase
    {
        public Player Controller { get; set; }
        public string FireTiming { get; set; }
        public Entity Source { get; set; }
    }

    // ==================== 保护/资源/特殊事件 ====================

    /// <summary>净化事件</summary>
    public class CleanseEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>添加法力事件</summary>
    public class AddManaEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ManaType ManaType { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>战斗事件</summary>
    public class FightEvent : GameEventBase
    {
        public Entity Attacker { get; set; }
        public Entity Defender { get; set; }
        public int DamageToAttacker { get; set; }
        public int DamageToDefender { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>属性类型枚举</summary>
    public enum StatType
    {
        Power,
        Life,
        Both
    }

    /// <summary>伤害类型枚举</summary>
    public enum DamageType
    {
        Normal,
        Combat,
        LifeLoss,
        Poison,
        Fire,
        Cold,
        Lightning
    }
}
