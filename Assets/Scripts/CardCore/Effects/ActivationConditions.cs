using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    #region 辅助接口

    // IHasKeywords 已在 CardTypes.cs 中定义
    // IHasAbilities 保留在此处

    /// <summary>
    /// 异能持有接口
    /// </summary>
    public interface IHasAbilities
    {
        /// <summary>检查是否拥有指定异能</summary>
        bool HasAbility(string abilityId);

        /// <summary>获取所有异能</summary>
        IEnumerable<string> GetAbilities();
    }

    #endregion

    #region 枚举定义

    /// <summary>
    /// 条件类型
    /// </summary>
    public enum ConditionType
    {
        #region 资源条件
        /// <summary>手牌数量下限</summary>
        MinCardsInHand,
        /// <summary>手牌数量上限</summary>
        MaxCardsInHand,
        /// <summary>场上卡牌数量下限</summary>
        MinCardsOnField,
        /// <summary>场上卡牌数量上限</summary>
        MaxCardsOnField,
        /// <summary>坟墓场卡牌数量条件</summary>
        CardsInGraveyard,
        /// <summary>牌库数量条件</summary>
        CardsInDeck,
        /// <summary>可用元素下限</summary>
        MinManaAvailable,
        /// <summary>特定颜色元素可用</summary>
        SpecificManaTypeAvailable,
        #endregion

        #region 实体条件
        /// <summary>控制者生命值条件</summary>
        ControllerHasLife,
        /// <summary>对手生命值条件</summary>
        OpponentHasLife,
        /// <summary>卡牌类型条件</summary>
        CardHasType,
        /// <summary>法力颜色条件</summary>
        CardHasManaType,
        /// <summary>卡牌已横置</summary>
        CardIsTapped,
        /// <summary>卡牌未横置</summary>
        CardIsUntapped,
        /// <summary>卡牌攻击力条件</summary>
        CardHasPower,
        /// <summary>卡牌生命值条件</summary>
        CardHasLife,
        /// <summary>拥有关键词</summary>
        HasKeyword,
        /// <summary>拥有异能</summary>
        HasAbility,
        #endregion

        #region 时点条件
        /// <summary>每回合一次</summary>
        OncePerTurn,
        /// <summary>仅主要阶段</summary>
        OnlyMainPhase,
        /// <summary>仅自己回合</summary>
        OnlyOwnTurn,
        /// <summary>仅对手回合</summary>
        OnlyOpponentTurn,
        /// <summary>本局首次</summary>
        FirstTimeThisGame,
        /// <summary>本回合首次</summary>
        FirstTimeThisTurn,
        /// <summary>战斗中</summary>
        DuringCombat,
        /// <summary>非战斗中</summary>
        NotDuringCombat,
        #endregion

        #region 场地条件
        /// <summary>场上有特定类型的卡</summary>
        FieldHasCardType,
        /// <summary>对手场上有特定类型的卡</summary>
        OpponentFieldHasCardType,
        /// <summary>手牌中有特定类型的卡</summary>
        HandHasCardType,
        /// <summary>墓地中有特定类型的卡</summary>
        GraveyardHasCardType,
        #endregion

        #region 伤害条件
        /// <summary>本回合造成伤害</summary>
        DamageDealtThisTurn,
        /// <summary>本回合受到伤害</summary>
        DamageTakenThisTurn,
        /// <summary>造成战斗伤害</summary>
        CombatDamageDealt,
        /// <summary>受到战斗伤害</summary>
        CombatDamageTaken,
        #endregion

        #region 战斗条件
        /// <summary>正在攻击</summary>
        Attacking,
        /// <summary>正在阻挡</summary>
        Blocking,
        /// <summary>本回合被阻挡</summary>
        BlockedThisTurn,
        /// <summary>被阻挡过</summary>
        WasBlocked,
        #endregion

        #region 连锁条件
        /// <summary>栈上有效果</summary>
        StackHasEffects,
        /// <summary>栈为空</summary>
        StackEmpty,
        /// <summary>拥有优先权</summary>
        HasPriority,
        #endregion

        #region 特殊条件
        /// <summary>自定义条件（通过ID匹配）</summary>
        Custom,
        /// <summary>复合条件 - 且</summary>
        And,
        /// <summary>复合条件 - 或</summary>
        Or,
        /// <summary>条件取反</summary>
        Not,
        #endregion
    }

    #endregion

    #region 前置条件定义

    /// <summary>
    /// 发动条件
    /// </summary>
    [Serializable]
    public class ActivationCondition
    {
        /// <summary>条件类型</summary>
        public ConditionType Type;

        /// <summary>数值参数</summary>
        public int Value;

        /// <summary>数值参数2</summary>
        public int Value2;

        /// <summary>卡牌超类型参数</summary>
        public Cardtype? SupertypeParam;

        /// <summary>法力类型参数</summary>
        public ManaType? ManaTypeParam;

        /// <summary>是否取反</summary>
        public bool Negate;

        /// <summary>子条件列表（用于复合条件）</summary>
        public List<ActivationCondition> SubConditions;

        /// <summary>自定义条件ID</summary>
        public string CustomConditionId;

        /// <summary>字符串参数（用于关键词、异能等条件）</summary>
        public string StringValue;

        /// <summary>
        /// 获取条件描述
        /// </summary>
        public string GetDescription()
        {
            string desc = Type switch
            {
                // 资源条件
                ConditionType.MinCardsInHand => $"手牌{Value}张以上",
                ConditionType.MaxCardsInHand => $"手牌{Value}张以下",
                ConditionType.MinCardsOnField => $"场上{Value}张以上",
                ConditionType.MaxCardsOnField => $"场上{Value}张以下",
                ConditionType.CardsInGraveyard => $"墓地{Value}张以上",
                ConditionType.CardsInDeck => $"牌库{Value}张以上",
                ConditionType.MinManaAvailable => $"可用元素{Value}以上",
                ConditionType.SpecificManaTypeAvailable => $"需要{ManaTypeParam}元素",

                // 实体条件
                ConditionType.ControllerHasLife => $"生命值{Value}以上",
                ConditionType.OpponentHasLife => $"对手生命值{Value}以下",
                ConditionType.CardHasType => $"卡牌类型为{SupertypeParam}",
                ConditionType.CardHasManaType => $"法力类型为{ManaTypeParam}",
                ConditionType.CardIsTapped => "已横置",
                ConditionType.CardIsUntapped => "未横置",
                ConditionType.CardHasPower => $"攻击力{Value}以上",
                ConditionType.CardHasLife => $"生命值{Value}以上",
                ConditionType.HasKeyword => $"拥有「{StringValue ?? "关键词"}」",
                ConditionType.HasAbility => $"拥有「{StringValue ?? "异能"}」",

                // 时点条件
                ConditionType.OncePerTurn => "每回合一次",
                ConditionType.OnlyMainPhase => "仅主要阶段",
                ConditionType.OnlyOwnTurn => "仅自己回合",
                ConditionType.OnlyOpponentTurn => "仅对手回合",
                ConditionType.FirstTimeThisGame => "本局首次",
                ConditionType.FirstTimeThisTurn => "本回合首次",
                ConditionType.DuringCombat => "战斗中",
                ConditionType.NotDuringCombat => "非战斗中",

                // 场地条件
                ConditionType.FieldHasCardType => $"场上有{SupertypeParam}",
                ConditionType.OpponentFieldHasCardType => $"对手场上有{SupertypeParam}",
                ConditionType.HandHasCardType => $"手牌中有{SupertypeParam}",
                ConditionType.GraveyardHasCardType => $"墓地中有{SupertypeParam}",

                // 伤害条件
                ConditionType.DamageDealtThisTurn => $"本回合造成{Value}点以上伤害",
                ConditionType.DamageTakenThisTurn => $"本回合受到{Value}点以上伤害",
                ConditionType.CombatDamageDealt => "造成战斗伤害",
                ConditionType.CombatDamageTaken => "受到战斗伤害",

                // 战斗条件
                ConditionType.Attacking => "正在攻击",
                ConditionType.Blocking => "正在阻挡",
                ConditionType.BlockedThisTurn => "本回合被阻挡",
                ConditionType.WasBlocked => "被阻挡过",

                // 连锁条件
                ConditionType.StackHasEffects => "栈上有效果",
                ConditionType.StackEmpty => "栈为空",
                ConditionType.HasPriority => "拥有优先权",

                // 复合条件
                ConditionType.And => "满足所有条件",
                ConditionType.Or => "满足任一条件",
                ConditionType.Not => "不满足条件",
                ConditionType.Custom => CustomConditionId ?? "自定义条件",
                _ => Type.ToString()
            };

            return Negate ? $"非{desc}" : desc;
        }

        #region 静态工厂方法

        /// <summary>手牌数量下限</summary>
        public static ActivationCondition MinHand(int min) => new()
        {
            Type = ConditionType.MinCardsInHand,
            Value = min
        };

        /// <summary>手牌数量上限</summary>
        public static ActivationCondition MaxHand(int max) => new()
        {
            Type = ConditionType.MaxCardsInHand,
            Value = max
        };

        /// <summary>场上数量下限</summary>
        public static ActivationCondition MinField(int min) => new()
        {
            Type = ConditionType.MinCardsOnField,
            Value = min
        };

        /// <summary>控制者生命值条件</summary>
        public static ActivationCondition ControllerLife(int min) => new()
        {
            Type = ConditionType.ControllerHasLife,
            Value = min
        };

        /// <summary>对手生命值条件</summary>
        public static ActivationCondition OpponentLife(int max) => new()
        {
            Type = ConditionType.OpponentHasLife,
            Value = max
        };

        /// <summary>每回合一次</summary>
        public static ActivationCondition OncePerTurn() => new()
        {
            Type = ConditionType.OncePerTurn
        };

        /// <summary>仅主要阶段</summary>
        public static ActivationCondition MainPhaseOnly() => new()
        {
            Type = ConditionType.OnlyMainPhase
        };

        /// <summary>仅自己回合</summary>
        public static ActivationCondition OwnTurnOnly() => new()
        {
            Type = ConditionType.OnlyOwnTurn
        };


        /// <summary>卡牌已横置</summary>
        public static ActivationCondition IsTapped() => new()
        {
            Type = ConditionType.CardIsTapped
        };

        /// <summary>卡牌未横置</summary>
        public static ActivationCondition IsUntapped() => new()
        {
            Type = ConditionType.CardIsUntapped
        };

        /// <summary>攻击力条件</summary>
        public static ActivationCondition HasPower(int min) => new()
        {
            Type = ConditionType.CardHasPower,
            Value = min
        };

        /// <summary>AND条件</summary>
        public static ActivationCondition And(params ActivationCondition[] conditions) => new()
        {
            Type = ConditionType.And,
            SubConditions = conditions.ToList()
        };

        /// <summary>OR条件</summary>
        public static ActivationCondition Or(params ActivationCondition[] conditions) => new()
        {
            Type = ConditionType.Or,
            SubConditions = conditions.ToList()
        };

        /// <summary>NOT条件</summary>
        public static ActivationCondition Not(ActivationCondition condition) => new()
        {
            Type = ConditionType.Not,
            SubConditions = new List<ActivationCondition> { condition }
        };

        /// <summary>可用元素下限</summary>
        public static ActivationCondition MinMana(int min) => new()
        {
            Type = ConditionType.MinManaAvailable,
            Value = min
        };

        /// <summary>特定颜色元素可用</summary>
        public static ActivationCondition HasManaType(ManaType manaType, int min = 1) => new()
        {
            Type = ConditionType.SpecificManaTypeAvailable,
            ManaTypeParam = manaType,
            Value = min
        };

        /// <summary>拥有关键词</summary>
        public static ActivationCondition HasKeywordCondition(string keywordId) => new()
        {
            Type = ConditionType.HasKeyword,
            StringValue = keywordId
        };

        /// <summary>拥有异能</summary>
        public static ActivationCondition HasAbilityCondition(string abilityId) => new()
        {
            Type = ConditionType.HasAbility,
            StringValue = abilityId
        };

        /// <summary>战斗中</summary>
        public static ActivationCondition InCombat() => new()
        {
            Type = ConditionType.DuringCombat
        };

        /// <summary>非战斗中</summary>
        public static ActivationCondition NotInCombat() => new()
        {
            Type = ConditionType.NotDuringCombat
        };

        /// <summary>本回合造成伤害</summary>
        public static ActivationCondition DealtDamageThisTurn(int min = 1) => new()
        {
            Type = ConditionType.DamageDealtThisTurn,
            Value = min
        };

        /// <summary>本回合受到伤害</summary>
        public static ActivationCondition TookDamageThisTurn(int min = 1) => new()
        {
            Type = ConditionType.DamageTakenThisTurn,
            Value = min
        };

        /// <summary>造成战斗伤害</summary>
        public static ActivationCondition DealtCombatDamage() => new()
        {
            Type = ConditionType.CombatDamageDealt
        };

        /// <summary>受到战斗伤害</summary>
        public static ActivationCondition TookCombatDamage() => new()
        {
            Type = ConditionType.CombatDamageTaken
        };

        /// <summary>正在攻击</summary>
        public static ActivationCondition IsAttacking() => new()
        {
            Type = ConditionType.Attacking
        };

        /// <summary>正在阻挡</summary>
        public static ActivationCondition IsBlocking() => new()
        {
            Type = ConditionType.Blocking
        };

        /// <summary>栈上有效果</summary>
        public static ActivationCondition StackNotEmpty() => new()
        {
            Type = ConditionType.StackHasEffects
        };

        /// <summary>栈为空</summary>
        public static ActivationCondition IsStackEmpty() => new()
        {
            Type = ConditionType.StackEmpty
        };

        /// <summary>拥有优先权</summary>
        public static ActivationCondition HasPriorityCondition() => new()
        {
            Type = ConditionType.HasPriority
        };

        #endregion
    }

    #endregion

    #region 条件检查器

    /// <summary>
    /// 条件检查上下文
    /// </summary>
    public class ConditionCheckContext
    {
        /// <summary>发动者</summary>
        public Player Activator { get; set; }

        /// <summary>主回合玩家</summary>
        public Player ActivePlayer { get; set; }

        /// <summary>当前阶段</summary>
        public PhaseType CurrentPhase { get; set; }

        /// <summary>当前回合数</summary>
        public int TurnNumber { get; set; }

        /// <summary>来源实体</summary>
        public Entity Source { get; set; }

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager { get; set; }

        /// <summary>效果已发动次数（本回合）</summary>
        public int ActivationsThisTurn { get; set; }

        /// <summary>效果已发动次数（本局）</summary>
        public int ActivationsThisGame { get; set; }

        /// <summary>是否在战斗阶段</summary>
        public bool IsInCombat { get; set; }

        /// <summary>本回合造成的伤害</summary>
        public int DamageDealtThisTurn { get; set; }

        /// <summary>本回合受到的伤害</summary>
        public int DamageTakenThisTurn { get; set; }

        /// <summary>是否造成过战斗伤害</summary>
        public bool HasDealtCombatDamage { get; set; }

        /// <summary>是否受到过战斗伤害</summary>
        public bool HasTakenCombatDamage { get; set; }

        /// <summary>是否正在攻击</summary>
        public bool IsAttacking { get; set; }

        /// <summary>是否正在阻挡</summary>
        public bool IsBlocking { get; set; }

        /// <summary>本回合是否被阻挡</summary>
        public bool WasBlockedThisTurn { get; set; }

        /// <summary>栈上效果数量</summary>
        public int StackSize { get; set; }

        /// <summary>当前优先权持有者</summary>
        public Player PriorityHolder { get; set; }

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool { get; set; }
    }

    /// <summary>
    /// 条件检查器
    /// </summary>
    public class ConditionChecker
    {
        private ZoneManager _zoneManager;

        public ConditionChecker(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
        }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        public bool Check(ActivationCondition condition, ConditionCheckContext context)
        {
            bool result = CheckInternal(condition, context);
            return condition.Negate ? !result : result;
        }

        /// <summary>
        /// 检查所有条件
        /// </summary>
        public bool CheckAll(List<ActivationCondition> conditions, ConditionCheckContext context)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            return conditions.All(c => Check(c, context));
        }

        private bool CheckInternal(ActivationCondition condition, ConditionCheckContext context)
        {
            switch (condition.Type)
            {
                #region 资源条件
                case ConditionType.MinCardsInHand:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        return container.GetCount(Zone.Hand) >= condition.Value;
                    }

                case ConditionType.MaxCardsInHand:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        return container.GetCount(Zone.Hand) <= condition.Value;
                    }

                case ConditionType.MinCardsOnField:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        return container.GetCount(Zone.Battlefield) >= condition.Value;
                    }

                case ConditionType.MaxCardsOnField:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        return container.GetCount(Zone.Battlefield) <= condition.Value;
                    }

                case ConditionType.CardsInGraveyard:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        return container.GetCount(Zone.Graveyard) >= condition.Value;
                    }

                case ConditionType.CardsInDeck:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        return container.GetCount(Zone.Deck) >= condition.Value;
                    }

                case ConditionType.MinManaAvailable:
                    {
                        var pool = context.ElementPool?.GetPool(context.Activator);
                        if (pool == null) return false;
                        int total = pool.AvailableMana.Values.Sum();
                        return total >= condition.Value;
                    }

                case ConditionType.SpecificManaTypeAvailable:
                    {
                        var pool = context.ElementPool?.GetPool(context.Activator);
                        if (pool == null) return false;
                        if (!pool.AvailableMana.TryGetValue(condition.ManaTypeParam ?? ManaType.Gray, out int count))
                            return false;
                        return count >= condition.Value;
                    }
                #endregion

                #region 实体条件
                case ConditionType.ControllerHasLife:
                    return context.Activator.Life >= condition.Value;

                case ConditionType.OpponentHasLife:
                    return context.Activator.Opponent.Life <= condition.Value;

                case ConditionType.CardHasType:
                    if (context.Source is Card card && card is IHasSupertype hasSupertype)
                        return hasSupertype.Supertype == condition.SupertypeParam;
                    return false;

                case ConditionType.CardHasManaType:
                    if (context.Source is Card c && c is IHasCost hasCost)
                        return hasCost.Cost.ContainsKey((int)condition.ManaTypeParam);
                    return false;

                case ConditionType.CardIsTapped:
                    if (context.Source is Card c0 && c0 is ITappable tappable)
                        return tappable.IsTapped;
                    return false;

                case ConditionType.CardIsUntapped:
                    if (context.Source is Card c1 && c1 is ITappable tappable2)
                        return !tappable2.IsTapped;
                    return true;

                case ConditionType.CardHasPower:
                    if (context.Source is Card c2 && c2 is IHasPower hasPower)
                        return hasPower.Power >= condition.Value;
                    return false;

                case ConditionType.CardHasLife:
                    if (context.Source is Card c3 && c3 is IHasLife hasLife)
                        return hasLife.Life >= condition.Value;
                    return false;

                case ConditionType.HasKeyword:
                    if (context.Source is Card c4 && c4 is IHasKeywords hasKeywords)
                        return hasKeywords.HasKeyword(condition.StringValue);
                    return false;

                case ConditionType.HasAbility:
                    if (context.Source is Card c5 && c5 is IHasAbilities hasAbilities)
                        return hasAbilities.HasAbility(condition.StringValue);
                    return false;
                #endregion

                #region 时点条件
                case ConditionType.OncePerTurn:
                    return context.ActivationsThisTurn == 0;

                case ConditionType.OnlyMainPhase:
                    return context.CurrentPhase == PhaseType.Main;

                case ConditionType.OnlyOwnTurn:
                    return context.Activator == context.ActivePlayer;

                case ConditionType.OnlyOpponentTurn:
                    return context.Activator != context.ActivePlayer;

                case ConditionType.FirstTimeThisGame:
                    return context.ActivationsThisGame == 0;

                case ConditionType.FirstTimeThisTurn:
                    return context.ActivationsThisTurn == 0;

                case ConditionType.DuringCombat:
                    return context.IsInCombat;

                case ConditionType.NotDuringCombat:
                    return !context.IsInCombat;
                #endregion

                #region 场地条件
                case ConditionType.FieldHasCardType:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        var cards = container.GetCards(Zone.Battlefield);
                        return cards.Any(c => c is IHasSupertype ct && ct.Supertype == condition.SupertypeParam);
                    }

                case ConditionType.OpponentFieldHasCardType:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator.Opponent);
                        var cards = container.GetCards(Zone.Battlefield);
                        return cards.Any(c => c is IHasSupertype ct && ct.Supertype == condition.SupertypeParam);
                    }

                case ConditionType.HandHasCardType:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        var cards = container.GetCards(Zone.Hand);
                        return cards.Any(c => c is IHasSupertype ct && ct.Supertype == condition.SupertypeParam);
                    }

                case ConditionType.GraveyardHasCardType:
                    {
                        var container = _zoneManager.GetZoneContainer(context.Activator);
                        var cards = container.GetCards(Zone.Graveyard);
                        return cards.Any(c => c is IHasSupertype ct && ct.Supertype == condition.SupertypeParam);
                    }
                #endregion

                #region 伤害条件
                case ConditionType.DamageDealtThisTurn:
                    return context.DamageDealtThisTurn >= condition.Value;

                case ConditionType.DamageTakenThisTurn:
                    return context.DamageTakenThisTurn >= condition.Value;

                case ConditionType.CombatDamageDealt:
                    return context.HasDealtCombatDamage;

                case ConditionType.CombatDamageTaken:
                    return context.HasTakenCombatDamage;
                #endregion

                #region 战斗条件
                case ConditionType.Attacking:
                    return context.IsAttacking;

                case ConditionType.Blocking:
                    return context.IsBlocking;

                case ConditionType.BlockedThisTurn:
                    return context.WasBlockedThisTurn;

                case ConditionType.WasBlocked:
                    return context.WasBlockedThisTurn;
                #endregion

                #region 连锁条件
                case ConditionType.StackHasEffects:
                    return context.StackSize > 0;

                case ConditionType.StackEmpty:
                    return context.StackSize == 0;

                case ConditionType.HasPriority:
                    return context.PriorityHolder == context.Activator;
                #endregion

                #region 复合条件
                case ConditionType.And:
                    if (condition.SubConditions == null) return true;
                    return condition.SubConditions.All(c => Check(c, context));

                case ConditionType.Or:
                    if (condition.SubConditions == null) return false;
                    return condition.SubConditions.Any(c => Check(c, context));

                case ConditionType.Not:
                    if (condition.SubConditions == null || condition.SubConditions.Count == 0) return true;
                    return !Check(condition.SubConditions[0], context);
                #endregion

                case ConditionType.Custom:
                    // TODO: 实现自定义条件检查
                    return true;

                default:
                    return true;
            }
        }
    }

    #endregion

    #region 条件追踪器

    /// <summary>
    /// 效果使用追踪器
    /// 追踪效果的发动次数等状态
    /// </summary>
    public class EffectUsageTracker
    {
        private Dictionary<string, int> _turnUsage = new Dictionary<string, int>();
        private Dictionary<string, int> _gameUsage = new Dictionary<string, int>();
        private int _currentTurn = -1;

        /// <summary>
        /// 新回合开始时调用
        /// </summary>
        public void OnNewTurn(int turnNumber)
        {
            if (_currentTurn != turnNumber)
            {
                _currentTurn = turnNumber;
                _turnUsage.Clear();
            }
        }

        /// <summary>
        /// 记录效果发动
        /// </summary>
        public void RecordActivation(string effectId)
        {
            if (!_turnUsage.ContainsKey(effectId))
                _turnUsage[effectId] = 0;
            _turnUsage[effectId]++;

            if (!_gameUsage.ContainsKey(effectId))
                _gameUsage[effectId] = 0;
            _gameUsage[effectId]++;
        }

        /// <summary>
        /// 获取本回合发动次数
        /// </summary>
        public int GetTurnUsage(string effectId)
        {
            return _turnUsage.GetValueOrDefault(effectId, 0);
        }

        /// <summary>
        /// 获取本局发动次数
        /// </summary>
        public int GetGameUsage(string effectId)
        {
            return _gameUsage.GetValueOrDefault(effectId, 0);
        }

        /// <summary>
        /// 重置（新游戏）
        /// </summary>
        public void Reset()
        {
            _turnUsage.Clear();
            _gameUsage.Clear();
            _currentTurn = -1;
        }
    }

    #endregion
}
