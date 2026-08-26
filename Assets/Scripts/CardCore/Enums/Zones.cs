using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 游戏区域
    /// </summary>
    public enum Zone
    {
        /// <summary>
        /// 手牌
        /// </summary>
        Hand,

        /// <summary>
        /// 战场
        /// </summary>
        Battlefield,

        /// <summary>
        /// 坟墓场
        /// </summary>
        Graveyard,

        /// <summary>
        /// 流放区
        /// </summary>
        Exile,

        /// <summary>
        /// 牌库
        /// </summary>
        Deck,

        /// <summary>
        /// 元素池（横置放置的手牌）
        /// </summary>
        ElementPool,

        /// <summary>
        /// 效果创造的衍生物临时卡牌
        /// </summary>
        None,

        /// <summary>
        /// 额外卡组（融合/同调/超量/连接卡）
        /// </summary>
        ExtraDeck,

        /// <summary>
        /// 场地区（场地魔法/场地效果）
        /// </summary>
        FieldZone,

        /// <summary>
        /// 灵摆区（灵摆刻度）
        /// </summary>
        PendulumZone,
    }

    /// <summary>
    /// 阶段类型
    /// </summary>
    public enum PhaseType
    {
        /// <summary>
        /// 准备阶段（含抽牌、可选放元素池）
        /// </summary>
        Standby,

        /// <summary>
        /// 主要阶段（含战斗）
        /// </summary>
        Main,

        /// <summary>
        /// 结束阶段
        /// </summary>
        End
    }

    /// <summary>
    /// 销毁原因
    /// </summary>
    public enum DestroyReason
    {
        /// <summary>
        /// 被效果销毁
        /// </summary>
        Destroyed,

        /// <summary>
        /// 战斗销毁
        /// </summary>
        Combat,

        /// <summary>
        /// 祭祀
        /// </summary>
        Sacrificed,

        /// <summary>
        /// 传说规则（同名卡）
        /// </summary>
        LegendaryRule
    }

    /// <summary>
    /// 状态变更类型
    /// </summary>
    public enum StateChangeType
    {
        /// <summary>
        /// 攻击力变更
        /// </summary>
        Power,

        /// <summary>
        /// 生命值变更
        /// </summary>
        Toughness,

        /// <summary>
        /// 文本变更
        /// </summary>
        Text,

        /// <summary>
        /// 颜色变更
        /// </summary>
        Color,

        /// <summary>
        /// 类型变更
        /// </summary>
        Type
    }

    /// <summary>
    /// 游戏结束原因
    /// </summary>
    public enum GameOverReason
    {
        /// <summary>
        /// 生命值归零
        /// </summary>
        LifeZero,

        /// <summary>
        /// 牌库耗尽
        /// </summary>
        DeckOut,

        /// <summary>
        /// 认输
        /// </summary>
        Concede,

        /// <summary>
        /// 超时
        /// </summary>
        TimeOut
    }

    /// <summary>
    /// 玩家行动位置
    /// </summary>
    public enum PlayLocation
    {
        /// <summary>
        /// 从手牌放置到战场
        /// </summary>
        HandToBattlefield,

        /// <summary>
        /// 从手牌直接使用
        /// </summary>
        HandUse
    }

    // 使用 EffectDefinition.cs 中的 StackObjectType 枚举

    /// <summary>
    /// 持续时间类型
    /// </summary>
    public enum DurationType
    {
        /// <summary>
        /// 一次性
        /// </summary>
        Once,
        /// <summary>
        /// 永久（整个对局）
        /// </summary>
        Permanent,

        /// <summary>
        /// 直到回合结束
        /// </summary>
        UntilEndOfTurn,

        /// <summary>
        /// 直到离开战场（包括被覆盖）
        /// </summary>
        UntilLeaveBattlefield,

        /// <summary>
        /// 只要（条件满足时）
        /// </summary>
        WhileCondition
    }

    /// <summary>
    /// 牌库检索后目的地
    /// </summary>
    public enum SearchDestination
    {
        /// <summary>
        /// 放入手牌
        /// </summary>
        ToHand,

        /// <summary>
        /// 放入战场
        /// </summary>
        ToBattlefield,

        /// <summary>
        /// 展示后放入手牌
        /// </summary>
        RevealThenHand,

        /// <summary>
        /// 展示后放入牌库顶
        /// </summary>
        RevealThenTop,

        /// <summary>
        /// 放入坟墓场
        /// </summary>
        ToGraveyard
    }

    /// <summary>
    /// 牌库位置
    /// </summary>
    public enum DeckPosition
    {
        /// <summary>
        /// 顶部
        /// </summary>
        Top,

        /// <summary>
        /// 底部
        /// </summary>
        Bottom,

        /// <summary>
        /// 随机位置
        /// </summary>
        Random
    }

    /// <summary>
    /// 无效化类型
    /// </summary>
    public enum NullifyType
    {
        /// <summary>
        /// 所有能力
        /// </summary>
        AllAbilities,

        /// <summary>
        /// 仅启动式能力
        /// </summary>
        ActivatedAbilities,

        /// <summary>
        /// 仅触发式能力
        /// </summary>
        TriggeredAbilities,

        /// <summary>
        /// 仅静态能力
        /// </summary>
        StaticAbilities
    }

    /// <summary>
    /// 复制类型
    /// </summary>
    public enum CopyType
    {
        /// <summary>
        /// 完全克隆（包括控制者）
        /// </summary>
        Clone,

        /// <summary>
        /// 镜像（控制者为原卡控制者）
        /// </summary>
        Mirror,

        /// <summary>
        /// 完全复制（独立控制）
        /// </summary>
        FullCopy
    }

    /// <summary>
    /// 特征类型
    /// </summary>
    public enum CharacteristicType
    {
        /// <summary>
        /// 法力颜色
        /// </summary>
        Color,

        /// <summary>
        /// 卡牌超类型
        /// </summary>
        Supertype,

        /// <summary>
        /// 子类型
        /// </summary>
        Subtype,

        /// <summary>
        /// 攻击力
        /// </summary>
        Power,

        /// <summary>
        /// 生命值（防御力）
        /// </summary>
        Toughness,

        /// <summary>
        /// 费用
        /// </summary>
        Cost,

        /// <summary>
        /// 文本
        /// </summary>
        Text
    }

    /// <summary>
    /// 特征定义类型
    /// </summary>
    public enum CharacteristicDefiningType
    {
        /// <summary>
        /// 定义颜色
        /// </summary>
        DefinesColor,

        /// <summary>
        /// 定义类型
        /// </summary>
        DefinesType,

        /// <summary>
        /// 定义子类型
        /// </summary>
        DefinesSubtype,

        /// <summary>
        /// 定义数值（攻击/生命）
        /// </summary>
        DefinesStats
    }

    /// <summary>
    /// 区域管理器
    /// </summary>
    public class ZoneManager
    {
        private Dictionary<Player, ZoneContainer> playerZones = new Dictionary<Player, ZoneContainer>();

        public ZoneManager()
        {
        }

        public void InitializePlayer(Player player)
        {
            if (!playerZones.ContainsKey(player))
            {
                playerZones[player] = new ZoneContainer(player);
            }
        }

        /// <summary>
        /// 获取玩家的区域容器
        /// </summary>
        public ZoneContainer GetZoneContainer(Player player)
        {
            return playerZones[player];
        }

        /// <summary>
        /// 将卡牌移动到指定区域
        /// </summary>
        public void MoveCard(Card card, Player controller, Zone fromZone, Zone toZone)
        {
            if (card == null)
                throw new ZoneOperationException(null, fromZone, toZone, "Cannot move a null card between zones.");
            if (fromZone == toZone)
                throw new ZoneOperationException(card, fromZone, toZone, $"Source and destination zones are the same ({fromZone}).");
            if (controller == null)
                throw new ZoneOperationException(card, fromZone, toZone, "Controller is null for zone move operation.");

            var container = GetZoneContainer(controller);
            if (container == null)
                throw new ZoneOperationException(card, fromZone, toZone, $"No zone container found for controller.");

            container.Move(card, fromZone, toZone);
        }

        /// <summary>
        /// 获取区域中的所有卡牌
        /// </summary>
        public List<Card> GetCards(Player player, Zone zone)
        {
            var container = GetZoneContainer(player);
            return container.GetCards(zone);
        }

        /// <summary>
        /// 检查卡牌是否在指定区域
        /// </summary>
        public bool IsCardInZone(Card card, Player player, Zone zone)
        {
            var container = GetZoneContainer(player);
            return container.IsCardInZone(card, zone);
        }
    }

    /// <summary>
    /// 玩家的区域容器
    /// </summary>
    public class ZoneContainer
    {
        public Player Owner { get; }

        // 区域使用「有序」列表存储：牌库/坟墓/放逐等区域的顺序具有规则意义
        // （抽牌/磨牌/检索/牌库顶操作均依赖 index 0 = 牌库顶的约定）。
        private Dictionary<Zone, List<Card>> zones = new Dictionary<Zone, List<Card>>();

        private static readonly System.Random _rng = new System.Random();

        public ZoneContainer(Player owner)
        {
            Owner = owner;
            foreach (Zone zone in Enum.GetValues(typeof(Zone)))
            {
                zones[zone] = new List<Card>();
            }
        }

        /// <summary>
        /// 移动卡牌到新区域（追加到目标区域末尾 / 牌库底）
        /// </summary>
        public void Move(Card card, Zone from, Zone to)
        {
            zones[from].Remove(card);
            if (!zones[to].Contains(card))
                zones[to].Add(card);
        }

        /// <summary>
        /// 移动卡牌到新区域（带位置参数，用于牌库顶/底/随机插入）
        /// </summary>
        public void Move(Card card, Zone from, Zone to, DeckPosition position)
        {
            zones[from].Remove(card);
            InsertByPosition(zones[to], card, position);
        }

        /// <summary>
        /// 将卡牌添加到指定区域（追加到末尾 / 牌库底）
        /// </summary>
        public void Add(Card card, Zone zone)
        {
            if (!zones[zone].Contains(card))
                zones[zone].Add(card);
        }

        /// <summary>
        /// 将卡牌添加到指定区域的指定位置（牌库顶/底/随机）
        /// </summary>
        public void Add(Card card, Zone zone, DeckPosition position)
        {
            InsertByPosition(zones[zone], card, position);
        }

        /// <summary>
        /// 从指定区域移除卡牌
        /// </summary>
        public void Remove(Card card, Zone zone)
        {
            zones[zone].Remove(card);
        }

        /// <summary>
        /// 获取区域中的所有卡牌（按区域顺序，index 0 = 牌库顶）
        /// </summary>
        public List<Card> GetCards(Zone zone)
        {
            return new List<Card>(zones[zone]);
        }

        /// <summary>
        /// 检查卡牌是否在指定区域
        /// </summary>
        public bool IsCardInZone(Card card, Zone zone)
        {
            return zones[zone].Contains(card);
        }

        /// <summary>
        /// 获取区域中卡牌数量
        /// </summary>
        public int GetCount(Zone zone)
        {
            return zones[zone].Count;
        }

        /// <summary>
        /// 清空指定区域
        /// </summary>
        public void Clear(Zone zone)
        {
            zones[zone].Clear();
        }

        /// <summary>
        /// 就地洗乱指定区域顺序（Fisher-Yates）。
        /// 必须作用于内部列表本身，否则洗牌不生效。
        /// </summary>
        public void Shuffle(Zone zone, System.Random rng = null)
        {
            rng ??= _rng;
            var list = zones[zone];
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>按 DeckPosition 将卡牌插入列表（Top=index0 牌库顶，Bottom=末尾，Random=随机）</summary>
        private static void InsertByPosition(List<Card> list, Card card, DeckPosition position)
        {
            if (list.Contains(card))
                return;

            switch (position)
            {
                case DeckPosition.Top:
                    list.Insert(0, card);
                    break;
                case DeckPosition.Random:
                    list.Insert(_rng.Next(list.Count + 1), card);
                    break;
                case DeckPosition.Bottom:
                default:
                    list.Add(card);
                    break;
            }
        }
    }

    /// <summary>
    /// ZoneManager 扩展方法 - 为效果处理器提供必要的区域操作API
    /// </summary>
    public static class ZoneManagerExtensions
    {
        /// <summary>获取玩家的区域容器（便捷方法）</summary>
        public static ZoneContainer GetZone(this ZoneManager zm, Player player, Zone zone)
        {
            if (zm == null || player == null) return null;
            return zm.GetZoneContainer(player);
        }

        /// <summary>移动实体卡牌到新区域</summary>
        public static void MoveCard(this ZoneManager zm, Entity entity, Zone fromZone, Zone toZone)
        {
            if (zm == null)
                throw new ZoneOperationException(entity as Card, fromZone, toZone, "ZoneManager is null.");
            if (entity == null)
                throw new ZoneOperationException(null, fromZone, toZone, "Cannot move a null entity between zones.");
            if (!(entity is Card card))
                throw new ZoneOperationException(null, fromZone, toZone, "Entity is not a Card and cannot be moved between zones.");

            var controller = card.GetController();
            if (controller == null)
                throw new ZoneOperationException(card, fromZone, toZone, "Card has no controller; cannot determine zone container.");

            var container = zm.GetZoneContainer(controller);
            if (container == null)
                throw new ZoneOperationException(card, fromZone, toZone, "No zone container found for card's controller.");

            container.Move(card, fromZone, toZone);
        }

        /// <summary>从牌库抽一张牌</summary>
        public static Card DrawCard(this ZoneManager zm, Player player)
        {
            if (zm == null || player == null) return null;

            var container = zm.GetZoneContainer(player);
            if (container == null) return null;

            var deck = container.GetCards(Zone.Deck);
            if (deck.Count == 0) return null;

            var card = deck[0];
            container.Move(card, Zone.Deck, Zone.Hand);
            return card;
        }

        /// <summary>从牌库顶磨一张牌（放入坟墓场）</summary>
        public static Card MillCard(this ZoneManager zm, Player player)
        {
            if (zm == null || player == null) return null;

            var container = zm.GetZoneContainer(player);
            if (container == null) return null;

            var deck = container.GetCards(Zone.Deck);
            if (deck.Count == 0) return null;

            var card = deck[0];
            container.Move(card, Zone.Deck, Zone.Graveyard);
            return card;
        }

        /// <summary>查看牌库顶N张牌（不移除）</summary>
        public static System.Collections.Generic.List<Card> GetTopCards(this ZoneManager zm, Player player, int count)
        {
            if (zm == null || player == null || count <= 0)
                return new System.Collections.Generic.List<Card>();

            var container = zm.GetZoneContainer(player);
            if (container == null) return new System.Collections.Generic.List<Card>();

            var deck = container.GetCards(Zone.Deck);
            var result = new System.Collections.Generic.List<Card>();
            for (int i = 0; i < Math.Min(count, deck.Count); i++)
            {
                result.Add(deck[i]);
            }
            return result;
        }

        /// <summary>洗牌</summary>
        public static void ShuffleDeck(this ZoneManager zm, Player player)
        {
            if (zm == null || player == null) return;

            var container = zm.GetZoneContainer(player);
            if (container == null) return;

            // 必须洗乱区域内部列表本身：GetCards 返回的是副本，洗它不会影响牌库
            container.Shuffle(Zone.Deck);
        }
    }
}
