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
        /// 场地区（场地魔法/场地效果）
        /// </summary>
        FieldZone,

        /// <summary>
        /// 发动区（万能结算位）：隐蔽区（手牌/卡组等）来源的发动先移入此区结算，
        /// 公开且可被指向（反制指向发动区而非来源区）；结算后按去向离区
        /// （入场型进战场、满则失败入墓；非入场型回原位）。场上卡原地发动，不经此区。
        /// 注意：新值只能追加在枚举末尾——SerializableRuntimeCardState 按 int 序列化，插中间会错位旧档。
        /// </summary>
        Activation,

        /// <summary>
        /// 额外区（2026-10-22 衍生物模板区）：卡组里 tags 含「衍生物」的模板卡开局由 InitGame
        /// 摘出落此区（照英雄技能卡摘出先例）——不进牌库、不会被抽到；纯展示/识别/引用面
        /// （SummonToken 按模板 id 从卡池解析实例化，不消费区内卡）。新值只能追加在枚举末尾。
        /// </summary>
        Extra,
    }

    /// <summary>
    /// 阶段类型
    /// </summary>
    public enum PhaseType
    {
        /// <summary>
        /// 准备阶段（纯自动：横置恢复、抽牌；放元素池已移至主阶段）
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
        /// 【墓碑 2026-10-09】祭祀——牺牲转直送非死亡（不发 CardDestroyEvent），本值无生产方；
        /// 枚举位保留防位序重编号。
        /// </summary>
        Sacrificed,

        /// <summary>
        /// 湮灭（彻底移除，直送除外区，不可复活）
        /// </summary>
        Annihilated,

        /// <summary>
        /// 摧毁（作用于无生命值单位：地牌/结界——不经死亡决策表，直送墓地）
        /// </summary>
        Smashed,

        /// <summary>
        /// 【墓碑 2026-10-09】摒弃——舍弃并档牺牲后无生产方（牺牲直送不发本事件）；枚举位保留防位序重编号。
        /// </summary>
        Abandoned,

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

        /// <summary>【墓碑 2026-10-13】牌库耗尽败因——零生产方（空库抽牌走疲劳），
        /// 早期游戏王式 DeckOut 设想未实装；枚举位保留防序列化位序重编号。</summary>
        DeckOut,

        /// <summary>
        /// 认输
        /// </summary>
        Concede,

        /// <summary>
        /// 超时
        /// </summary>
        TimeOut,

        /// <summary>
        /// 效果宣告胜利（终局原子：效果控制者的对手获胜）
        /// </summary>
        EffectVictory
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
        WhileCondition,

        /// <summary>
        /// 到对手回合结束（回合单位＝玩家回合，与全局回合数同源：
        /// 自己回合内施加，活过对手整个回合，于对手回合结束时失效——防御性持续档）
        /// </summary>
        UntilNextTurn,

        /// <summary>
        /// 指定回合数 N（N 存 DurationValue；施加当回合记第 1 回合，于第 N 个玩家回合结束时失效）。
        /// N=1 等价 UntilEndOfTurn，N=2 等价 UntilNextTurn。
        /// </summary>
        ForTurns
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
        /// 仅主动能力
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
        /// 定义数值（攻击/生命）
        /// </summary>
        DefinesStats
    }

    /// <summary>
    /// 区域管理器
    /// </summary>
    public class ZoneManager
    {
        /// <summary>
        /// 每玩家战场容量上限（= 单位区 2×6=12；2026-10-14 晚 8×7 缩盘定案）。
        /// 计数语义，与棋盘格子无关——棋盘只是核心状态的派生表现层
        ///（BoardLayout.UnitCellsPerPlayer 已改单源引用本值，2026-10-13；
        /// 数值迁表：ValueSystemConfig GameRules.BattlefieldCapacityPerPlayer）。
        /// </summary>
        public static int BattlefieldCapacityPerPlayer
            => (int)ValueSystemConfigManager.Instance.GetOrCreateConfig().GameRulesConfig.BattlefieldCapacityPerPlayer;

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

        /// <summary>战场是否还有空位（手动出牌预检与结算时入场的容量闸门）</summary>
        public bool HasBattlefieldSpace(Player player)
        {
            return playerZones.TryGetValue(player, out var container)
                   && container.GetCount(Zone.Battlefield) < BattlefieldCapacityPerPlayer;
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
        // （抽牌/送墓/检索/牌库顶操作均依赖 index 0 = 牌库顶的约定）。
        private Dictionary<Zone, List<Card>> zones = new Dictionary<Zone, List<Card>>();

        // 可重定种子（2026-09-10）：验证器/无头对拍需要同种子复现（M3 联网里程碑同款要求）；
        // 默认时间种子不变，Reseed 后同种子 → 同洗牌序列。
        // 2026-10-26 检查点口（回合快照前置，同 GameRng 口径）：静态洗牌流调用计数 + Capture/Restore。
        private static System.Random _rng = new System.Random();
        private static int _shuffleSeed;
        private static int _shuffleCalls;
        private static bool _shuffleSeeded;

        /// <summary>重定洗牌随机种子（验证器固定种子复现 / 对拍用）；影响全部 ZoneContainer。</summary>
        public static void Reseed(int seed)
        {
            _rng = new System.Random(seed);
            _shuffleSeed = seed;
            _shuffleCalls = 0;
            _shuffleSeeded = true;
        }

        /// <summary>洗牌流检查点（回合快照采集用）。</summary>
        public static RngCheckpoint CaptureShuffle() => new RngCheckpoint(_shuffleSeeded, _shuffleSeed, _shuffleCalls);

        /// <summary>恢复洗牌流检查点（预留口，重开本回合接线；未钉种子流不动作）。</summary>
        public static void RestoreShuffle(RngCheckpoint cp)
        {
            if (!cp.Seeded) return;
            Reseed(cp.Seed);
            for (int i = 0; i < cp.Calls; i++)
                _rng.Next(int.MaxValue); // 与被计数的调用同款：每次消耗一个流位
        }

        /// <summary>静态洗牌流取数（计数经此口，外部注入 rng 的 Shuffle 不计——它不在重播流上）。</summary>
        private static int ShuffleNext(int maxExclusive)
        {
            _shuffleCalls++;
            return _rng.Next(maxExclusive);
        }

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
            card._zone = to; // 区域真源维护：_zone 随容器移动同步（见类尾注释）
            OnCardMoved(card, from, to);
        }

        /// <summary>
        /// 移动卡牌到新区域（带位置参数，用于牌库顶/底/随机插入）
        /// </summary>
        public void Move(Card card, Zone from, Zone to, DeckPosition position)
        {
            zones[from].Remove(card);
            InsertByPosition(zones[to], card, position);
            card._zone = to;
            OnCardMoved(card, from, to);
        }

        /// <summary>
        /// 换区统一清理（定案默认持续：未写持续时间的指示物=持续到移动所属区域）：
        /// 指示物属性先反向回写再清空；入手重置宣言"已展示"标记；离场发事件并解除变形。
        /// 【发动区豁免（定案）】发动区=栈的物理呈现/暂存区，不是卡的真实归属——
        /// 进发动区（隐蔽区来源声明）与声明失败回滚（退回来源区）不清指示物：
        /// cast 付费发生在发动区内，费用指示物必须存活到结算；结算后的真实去向
        /// （入场/入墓/被无效化送墓/支付失败）才是换区清除点。
        /// </summary>
        private void OnCardMoved(Card card, Zone from, Zone to)
        {
            bool viaActivationStaging = to == Zone.Activation
                                        || (from == Zone.Activation && to == Zone.Hand);
            if (from != to && !viaActivationStaging)
                CardCore.Attribute.CounterRules.ClearAll(card); // 攻/血指示物离场消失、费用指示物离手消失

            if (to == Zone.Hand && from != Zone.Hand)
            {
                card._isRevealed = false; // 入手重置：宣言确认手牌的"已展示"标记
                PublishCardEnterHand(card, from);
            }
            if (from == Zone.Battlefield && to != Zone.Battlefield)
                PublishCardLeaveBattlefield(card, to);
            if (from == Zone.Battlefield && to != Zone.Battlefield)
                CardCore.Attribute.MorphSystem.RestoreIfMorphed(card); // 变形解除（2026-10-13 重做）：
            // 还原钩子实际由上方 ClearAll→CounterRules.ClearCounters（变形指示物被清）驱动——
            // 此处直调为无指示物残留时的兜底（幂等：无快照即 no-op），保序不变。
            // 临时关键词换区清（三轨制定案）：置于解除之后——防快照复活已清层；
            // 同受发动区豁免约束（cast 付费期间临时层存活）。
            if (from != to && !viaActivationStaging)
                CardCore.Attribute.KeywordRules.ClearZoneKeywords(card);
        }

        /// <summary>
        /// 卡牌加入手牌事件（容器层统一发布；经 GameCore 路由，触发/层引擎可见）。
        /// IsDraw 由 ZoneManagerExtensions.DrawCard 的静态标记位提供（抽牌路径）。
        /// </summary>
        private void PublishCardEnterHand(Card card, Zone from)
        {
            var e = new CardEnterHandEvent
            {
                Player = card.GetController(),
                Card = card,
                FromZone = from,
                IsDraw = ZoneManagerExtensions.IsDrawMove
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }

        /// <summary>
        /// 卡牌离场事件（战场 → 任何非战场区；经 GameCore 路由，OnLeaveBattlefield 触发时点可见）。
        /// </summary>
        private void PublishCardLeaveBattlefield(Card card, Zone to)
        {
            var e = new CardLeaveBattlefieldEvent
            {
                Card = card,
                Controller = card.GetController(),
                Destination = to
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }

        /// <summary>
        /// 将卡牌添加到指定区域（追加到末尾 / 牌库底）
        /// </summary>
        public void Add(Card card, Zone zone)
        {
            if (!zones[zone].Contains(card))
                zones[zone].Add(card);
            card._zone = zone;
        }

        /// <summary>
        /// 将卡牌添加到指定区域的指定位置（牌库顶/底/随机）
        /// </summary>
        public void Add(Card card, Zone zone, DeckPosition position)
        {
            InsertByPosition(zones[zone], card, position);
            card._zone = zone;
        }

        /// <summary>
        /// 从指定区域移除卡牌
        /// </summary>
        public void Remove(Card card, Zone zone)
        {
            zones[zone].Remove(card);
            card._zone = Zone.None;
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
            foreach (var card in zones[zone])
                card._zone = Zone.None;
            zones[zone].Clear();
        }

        // 【区域真源约定】Card._zone 由本容器的 Move/Add/Remove/Clear 统一维护，
        // 调用方不再手动 SetZone（历史上仅部分 handler 手动同步，导致 _zone 与容器
        // 真实位置脱节、卡双挂在旧区域列表）。例外：RuntimeCardStateDTO.ApplyToCard
        // 直写 _zone 重建快照——快照恢复=状态重建，不发区域事件，保持现状。

        /// <summary>
        /// 就地洗乱指定区域顺序（Fisher-Yates）。
        /// 必须作用于内部列表本身，否则洗牌不生效。
        /// </summary>
        public void Shuffle(Zone zone, System.Random rng = null)
        {
            var list = zones[zone];
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng != null ? rng.Next(n + 1) : ShuffleNext(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        /// 就地重排区域顶部的卡序（观星/占卜用）：orderedTop 即新的顶部顺序（index0=顶）。
        /// 卡必须全部已在本区域内；未参与重排的卡保持原相对顺序跟在后面。
        /// </summary>
        public void ReorderTop(Zone zone, List<Card> orderedTop)
        {
            if (orderedTop == null || orderedTop.Count == 0) return;
            var list = zones[zone];
            if (orderedTop.Count > list.Count) return; // 防御：重排数超出区域总量

            var set = new HashSet<Card>(orderedTop);
            var rest = new List<Card>();
            foreach (var c in list)
                if (!set.Contains(c))
                    rest.Add(c);

            for (int i = 0; i < orderedTop.Count; i++) list[i] = orderedTop[i];
            for (int i = 0; i < rest.Count; i++) list[orderedTop.Count + i] = rest[i];
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
                    list.Insert(ShuffleNext(list.Count + 1), card);
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

        /// <summary>
        /// 当前 Move 是否处于抽牌路径（ZoneContainer 发布 CardEnterHandEvent 时读取）。
        /// 仅 DrawCard 扩展在移动期间置位；「非抽牌形式入手」类计数以此区分来源。
        /// </summary>
        internal static bool IsDrawMove;

        /// <summary>从牌库抽一张牌。牌库为空时不抽牌，改为疲劳（第 N 次疲劳造成 N 点递增伤害）。
        /// 抽卡后经统一路由发布 CardDrawEvent——抽卡时点（OnDraw）对所有触发可见。</summary>
        public static Card DrawCard(this ZoneManager zm, Player player, bool firstDrawOfTurn = false)
        {
            if (zm == null || player == null) return null;

            var container = zm.GetZoneContainer(player);
            if (container == null) return null;

            var deck = container.GetCards(Zone.Deck);
            if (deck.Count == 0)
            {
                ApplyFatigue(player);
                return null;
            }

            var card = deck[0];
            bool prev = IsDrawMove;
            IsDrawMove = true;
            try
            {
                container.Move(card, Zone.Deck, Zone.Hand);
            }
            finally
            {
                IsDrawMove = prev;
            }

            // 抽卡时点：经统一路由（触发/层引擎可见）单源发布——回合抽牌与效果抽牌共用此口
            // （2026-10-15：DrawCardHandler 侧的重复发布已删，防效果抽牌同卡双事件）
            var drawEvent = new CardDrawEvent
            {
                Player = player,
                DrawnCard = card,
                DrawCount = 1,
                FirstDrawOfTurn = firstDrawOfTurn
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(drawEvent);
            else EventManager.Instance.Publish(drawEvent);
            return card;
        }

        /// <summary>
        /// 疲劳结算（炉石式）：空卡组抽牌时，第 N 次疲劳对玩家造成 N 点递增伤害。
        /// 经 GameCore 统一路由发布 FatigueEvent / LifeChangeEvent；生命归 0 立即发 GameOverEvent（LifeZero）。
        /// </summary>
        private static void ApplyFatigue(Player player)
        {
            // 规则修改类效果（OCP）：疲劳先经替代引擎（免疫 = 替代为 Damage 0 → 不计数、不结算）
            var routedEvent = new CardCore.Attribute.FatigueEvent { Player = player, Damage = player.FatigueCount + 1 };
            if (GameCore.Instance?.ReplacementEngine != null)
            {
                routedEvent = GameCore.Instance.ReplacementEngine
                    .CheckReplacements(routedEvent).GetFinalEvent() as CardCore.Attribute.FatigueEvent ?? routedEvent;
            }
            if (routedEvent.Damage <= 0) return;

            player.FatigueCount++;
            int damage = routedEvent.Damage;
            int oldLife = player.Life;
            int newLife = oldLife - damage;
            if (newLife < 0) newLife = 0;
            player.Life = newLife;

            void Publish<T>(T e) where T : IGameEvent
            {
                if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
                else EventManager.Instance.Publish(e);
            }

            Publish(new CardCore.Attribute.FatigueEvent { Player = player, Damage = damage });
            Publish(new LifeChangeEvent { Player = player, OldLife = oldLife, NewLife = newLife });

            if (newLife <= 0)
            {
                // 统一发布口：只发一次守卫 + TotalTurns 补全（未接线 GameCore 时退化直发）
                if (GameCore.Instance != null)
                    GameCore.Instance.PublishGameOverOnce(player.Opponent, GameOverReason.LifeZero);
                else
                    Publish(new GameOverEvent
                    {
                        Winner = player.Opponent,
                        Loser = player,
                        Reason = GameOverReason.LifeZero,
                    });
            }
        }

        /// <summary>送墓：从牌库顶送一张牌入墓地</summary>
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

        // ======================= 入场容量闸门 =======================
        // 手动出牌（PlayCard）满场 = 预检拒绝（MD 式，卡留手牌不付费）；
        // 结算时入场（发动区通过/检索/复活/召唤/衍生物/副本）满场 = 失败入墓。

        /// <summary>
        /// 尝试让已有卡牌入场（结算时入场的统一容量闸门，PlayCard 发动区流转的共用出口）：
        /// 己方战场有空位 → 移入战场并发布 CardPutToBattlefieldEvent（来自发动区时先发离区事件）；
        /// 战场已满 → 入场失败：卡入墓（来源即墓地则原地不动）并发 CardActivationFailedEvent。
        /// 设计规则：发动通过后要进战场时失败 → 进墓地。
        /// </summary>
        /// <returns>true = 成功入场</returns>
        public static bool TryMoveToBattlefield(this ZoneManager zm, Card card, Player controller, Zone fromZone, EnterSource enterSource = EnterSource.None)
        {
            if (zm == null || card == null || controller == null) return false;
            var container = zm.GetZoneContainer(controller);
            if (container == null) return false;

            if (!zm.HasBattlefieldSpace(controller))
            {
                if (fromZone != Zone.Graveyard)
                    container.Move(card, fromZone, Zone.Graveyard);
                PublishEntryEvent(new CardActivationFailedEvent
                {
                    Card = card,
                    Controller = controller,
                    FromZone = fromZone,
                    Reason = "BattlefieldFull",
                });
                return false;
            }

            container.Move(card, fromZone, Zone.Battlefield);

            // 入场即可用（2026-10-17 定案：取消横置入场，召唤失调退役）——显式复位而非沿用旧值：
            // 复活路径的卡可能带着横置态死亡，重入场必须复位为可用。横置的唯一来源=战斗后代价与
            // 效果横置（沉睡/冻结/横置原子）；冲锋/突袭的登场解横置效果行就此成为空操作（真实卡池 0 张）。
            card._isTapped = false;

            if (fromZone == Zone.Activation)
                PublishEntryEvent(new CardLeaveActivationEvent
                {
                    Card = card,
                    Controller = controller,
                    ToZone = Zone.Battlefield,
                });

            // 入场刷新（2026-09-09 定案）：真实入场对 Printed 轨做卡面差集补齐（消耗项随入场恢复）——
            // 只挂统一出口（真换区）：复生原地留场/控制权变更容器直移均不经此，天然不触发。
            CardCore.Attribute.KeywordRules.RefreshPrintedKeywordsOnEntry(card);

            // 触发式注册统一出口（时点接线定案）：入场事件发布前完成注册，
            // 保证入场卡自己的 OnPlay/OnSummon 能吃到自己的入场事件（RegisterEffect 幂等）
            GameActions.RegisterCardTriggeredEffects(GameCore.Instance, card, controller);

            // 入场事件带来源（时点接线定案）：未显式指定时按 FromZone 派生
            PublishEntryEvent(new CardPutToBattlefieldEvent
            {
                Card = card,
                Controller = controller,
                Tapped = card._isTapped,
                FromZone = fromZone,
                Source = enterSource != EnterSource.None ? enterSource : DeriveEnterSource(fromZone),
            });
            return true;
        }

        /// <summary>按来源区派生进场来源（TryMoveToBattlefield 未显式指定时的兜底）</summary>
        private static EnterSource DeriveEnterSource(Zone fromZone)
        {
            switch (fromZone)
            {
                case Zone.Activation: return EnterSource.CastPlayed;   // 经发动区打出
                case Zone.Graveyard: return EnterSource.Revived;       // 墓地复活
                default: return EnterSource.SummonedByEffect;          // 牌组检索等效果直入
            }
        }

        /// <summary>
        /// 尝试把新建卡牌（衍生物/副本——尚未在任何区域）加入战场：
        /// 满则进墓地并发失败事件（保持与在场卡同一"满则入墓"规则）。
        /// 成功路径发布 CardPutToBattlefieldEvent（时点接线定案：token 进场同样触发
        /// OnSummon/OnOtherCreatureEnter——"入场事件留给呈现层"的旧注释已推翻）。
        /// </summary>
        public static bool TryAddToBattlefield(this ZoneManager zm, Card card, Player controller, EnterSource enterSource = EnterSource.TokenSpawned)
        {
            if (zm == null || card == null || controller == null) return false;
            var container = zm.GetZoneContainer(controller);
            if (container == null) return false;

            if (!zm.HasBattlefieldSpace(controller))
            {
                container.Add(card, Zone.Graveyard);
                PublishEntryEvent(new CardActivationFailedEvent
                {
                    Card = card,
                    Controller = controller,
                    FromZone = Zone.None,
                    Reason = "BattlefieldFull",
                });
                return false;
            }

            container.Add(card, Zone.Battlefield);
            // 入场即可用（2026-10-17 定案：取消横置入场）——衍生物/副本同普通生物；
            // 横置仅来自战斗后代价与效果横置（沉睡/冻结/横置原子）
            card._isTapped = false;

            // 入场刷新（2026-09-09 定案）：与 TryMoveToBattlefield 同口径（统一出口，真入场才刷新）
            CardCore.Attribute.KeywordRules.RefreshPrintedKeywordsOnEntry(card);

            // 触发式注册统一出口（时点接线定案）：token/副本入场同样注册自身触发式（幂等）
            GameActions.RegisterCardTriggeredEffects(GameCore.Instance, card, controller);

            PublishEntryEvent(new CardPutToBattlefieldEvent
            {
                Card = card,
                Controller = controller,
                Tapped = card._isTapped,
                FromZone = Zone.None,
                Source = enterSource,
            });
            return true;
        }

        /// <summary>入场/发动区事件统一经 GameCore 路由（Replacement→Event→Trigger→Layer），未接线时退化为事件总线</summary>
        private static void PublishEntryEvent<T>(T e) where T : IGameEvent
        {
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }
    }
}
