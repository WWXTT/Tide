using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>
    /// 实体引用双轨表达（M1 协议定案 2026-09-10）：
    /// 卡 = RuntimeId（Entity 构造期全局自增，进程内唯一）；玩家 = 座位号（0/1）。
    /// CardId 为卡表模板 ID，仅供客户端查表显示——不可当实例身份（BuildDeck 副本共享）。
    /// </summary>
    [MemoryPackable]
    public partial class NetEntityRef
    {
        [MemoryPackOrder(TagTable.NER_RuntimeId)]
        public uint RuntimeId;

        [MemoryPackOrder(TagTable.NER_Seat)]
        public int Seat;

        [MemoryPackOrder(TagTable.NER_IsPlayer)]
        public bool IsPlayer;

        [MemoryPackOrder(TagTable.NER_CardId)]
        public string CardId;
    }

    /// <summary>引用构造（Entity → NetEntityRef）。</summary>
    public static class NetEntityMapper
    {
        public static NetEntityRef None { get; } = new NetEntityRef { Seat = -1 };

        public static NetEntityRef FromCard(Card card)
        {
            if (card == null) return None;
            return new NetEntityRef
            {
                RuntimeId = card.RuntimeId,
                Seat = SeatOf(card.GetController()),
                IsPlayer = false,
                CardId = card.ID,
            };
        }

        public static NetEntityRef FromPlayer(Player player)
        {
            if (player == null) return None;
            return new NetEntityRef
            {
                Seat = SeatOf(player),
                IsPlayer = true,
                CardId = null,
            };
        }

        public static NetEntityRef FromEntity(Entity entity)
            => entity is Player p ? FromPlayer(p) : FromEntityCard(entity);

        private static NetEntityRef FromEntityCard(Entity entity)
            => entity is Card c ? FromCard(c) : None;

        /// <summary>座位号：P1=0 / P2=1 / 非对局玩家=-1（按 GameCore 单例判等）。</summary>
        public static int SeatOf(Player player)
        {
            var core = GameCore.Instance;
            if (core == null || player == null) return -1;
            if (ReferenceEquals(player, core.Player1)) return 0;
            if (ReferenceEquals(player, core.Player2)) return 1;
            return -1;
        }
    }

    /// <summary>
    /// RuntimeId → Entity 运行时字典（解析缓存）。
    /// miss 时全区域扫描重建（双方 Hand/Battlefield/Graveyard/Exile/Deck/ElementPool/Activation/
    /// ExtraDeck/FieldZone + 发动区，≤200 张，O(n) 可忽略）。
    /// 卡在发动区（cast 上栈期间）也在 Zone 扫描覆盖内——MoveCard 先进 Activation 再 PushCardCast。
    /// </summary>
    public static class NetEntityDirectory
    {
        private static readonly Dictionary<uint, Entity> _byId = new Dictionary<uint, Entity>();

        /// <summary>登记（幂等；同 ID 重复登记同一对象为 no-op）。</summary>
        public static void Register(Entity entity)
        {
            if (entity == null) return;
            _byId[entity.RuntimeId] = entity;
        }

        public static void Clear() => _byId.Clear();

        /// <summary>按引用取实体：玩家按座位；卡按 RuntimeId（miss 全区域扫描）。</summary>
        public static Entity Resolve(GameCore core, NetEntityRef reference)
        {
            if (core == null || reference == null) return null;
            if (reference.IsPlayer)
                return SeatToPlayer(core, reference.Seat);
            return ResolveCard(core, reference.RuntimeId);
        }

        public static Player SeatToPlayer(GameCore core, int seat)
        {
            if (core == null) return null;
            return seat == 0 ? core.Player1 : seat == 1 ? core.Player2 : null;
        }

        /// <summary>按 RuntimeId 取卡（miss 时全区域扫描重建缓存）。
        /// 缓存引用恒有效：RuntimeId 构造期分配、进程内不复用；死亡入墓的卡仍是合法解析对象
        /// （墓地打出等场景），不做存活过滤——约束由 GameActions 各入口自己校验。</summary>
        public static Card ResolveCard(GameCore core, uint runtimeId)
        {
            if (core == null || runtimeId == 0) return null;
            if (_byId.TryGetValue(runtimeId, out var cached) && cached is Card cachedCard)
                return cachedCard;

            foreach (var card in EnumerateAllCards(core))
            {
                Register(card);
                if (card.RuntimeId == runtimeId)
                    return card;
            }
            return null;
        }

        /// <summary>批量解析；任一失败返回 false（严格口径：不做静默丢目标）。</summary>
        public static bool TryResolveAll(GameCore core, NetEntityRef[] references, out List<Entity> entities)
        {
            entities = new List<Entity>();
            if (references == null) return true;
            foreach (var reference in references)
            {
                var entity = Resolve(core, reference);
                if (entity == null) return false;
                entities.Add(entity);
            }
            return true;
        }

        /// <summary>双方全部区域卡枚举（token/发动区/元素池内的卡都经 ZoneManager 可见）。</summary>
        public static IEnumerable<Card> EnumerateAllCards(GameCore core)
        {
            if (core == null) yield break;
            foreach (var player in new[] { core.Player1, core.Player2 })
            {
                if (player == null) continue;
                foreach (Zone zone in Enum.GetValues(typeof(Zone)))
                {
                    var cards = core.ZoneManager.GetCards(player, zone);
                    if (cards == null) continue;
                    foreach (var card in cards)
                        yield return card;
                }
            }
        }
    }
}
