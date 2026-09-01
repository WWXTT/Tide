using System;
using System.Collections.Generic;
using CardCore;

namespace GameBoard
{
    /// <summary>规则档位：Tactical = 故事模式（落差参与距离）/ Flat = P2P（完全平面）</summary>
    public enum BoardRulesMode
    {
        Tactical,
        Flat,
    }

    /// <summary>
    /// 对战棋盘的派生状态层：占用 = 核心区域列表的确定性纯函数。
    ///
    /// 【架构铁律】核心数据不与棋盘绑定（方便传输与断线重连）：
    /// - 单向引用：本类只读 GameCore 的区域列表，CardCore 不感知棋盘；
    /// - 占用分配完全确定性：按区域列表顺序 first-free 落格，Resync 两次结果逐格一致；
    /// - 重连/观战 late-join：核心状态同步完成后调一次 Resync() 即重建全部占用，
    ///   棋盘零字节上线；Card 零坐标字段、GameCore 零装配改动。
    ///
    /// 战棋预留（Phase A 只有数据与公式，无玩法接线）：默认攻击距离 2、
    /// 攻击距离 = 六距 + (Tactical ? |落差| : 0)；随从移动/飞行/远程加程/地形增益
    /// 等玩法落地时在本层加卡牌维度的覆盖表（或迁关键词系统），不上 Card 核心字段。
    ///
    /// 控制权变更（场内迁移）不受容量闸门：满场偷取仍生效，占用按新归属重排（见
    /// SecondBatchEffectHandlers.ChangeControl 的定案注释）。
    /// </summary>
    public class BoardState : IDisposable
    {
        /// <summary>默认攻击距离（平地 2 格 / 落差 1 的 1 格——两组设计示例都恰好取等）</summary>
        public const int DefaultAttackRange = 2;

        public BoardRulesMode Mode { get; set; } = BoardRulesMode.Flat;

        /// <summary>拼合后的整场地形（表现 + 战棋预留数据源）</summary>
        public ComposedField Field { get; }

        private readonly GameCore _core;
        private readonly Player _p1; // 布局归属 0（下半场）
        private readonly Player _p2; // 布局归属 1（上半场）

        // 占用索引：单写入口（Resync），双向成对维护
        private readonly Dictionary<int, Card> _occupant = new Dictionary<int, Card>(); // cellIndex -> 卡（单位/地牌）
        private readonly Dictionary<Card, int> _cellOf = new Dictionary<Card, int>();   // 反向

        // 发动格叠放（= 栈的物理呈现）：Phase A 暂态在单次 PlayCard 内，通常为空；
        // StackEngine 路径接入后此处承载多张待结算卡
        private readonly List<Card> _activation0 = new List<Card>();
        private readonly List<Card> _activation1 = new List<Card>();

        private bool _autoResync;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public BoardState(GameCore core, Player p1, Player p2,
            HalfFieldData half1 = null, HalfFieldData half2 = null)
        {
            _core = core;
            _p1 = p1;
            _p2 = p2;
            Field = ComposedField.Compose(half1, half2);
        }

        // ======================================== 重建（重连即重建） ========================================

        /// <summary>
        /// 从核心区域列表重建全部占用（幂等、确定性——同状态必得同布局）。
        /// 战场列表 → 己方单位区格序 first-free；元素池列表 → 地牌行格序；
        /// 发动区列表 → 发动格叠放。容量外（控制权迁移等边缘）不落格、不抛错。
        /// </summary>
        public void Resync()
        {
            _occupant.Clear();
            _cellOf.Clear();
            _activation0.Clear();
            _activation1.Clear();

            AssignCells(_p1, Zone.Battlefield, BoardLayout.UnitCells(0));
            AssignCells(_p2, Zone.Battlefield, BoardLayout.UnitCells(1));
            AssignCells(_p1, Zone.ElementPool, BoardLayout.LandCells(0));
            AssignCells(_p2, Zone.ElementPool, BoardLayout.LandCells(1));

            AppendActivation(_p1, _activation0);
            AppendActivation(_p2, _activation1);
        }

        private void AssignCells(Player player, Zone zone, IReadOnlyList<(int x, int z)> cells)
        {
            var cards = SafeGetCards(player, zone);
            if (cards == null) return;

            int n = Math.Min(cards.Count, cells.Count);
            for (int i = 0; i < n; i++)
            {
                int idx = BoardMath.Index(cells[i].x, cells[i].z);
                _occupant[idx] = cards[i];
                _cellOf[cards[i]] = idx;
            }
        }

        private void AppendActivation(Player player, List<Card> pile)
        {
            var cards = SafeGetCards(player, Zone.Activation);
            if (cards != null) pile.AddRange(cards);
        }

        /// <summary>表现层不得抛错：玩家未注册区域容器时返回 null</summary>
        private List<Card> SafeGetCards(Player player, Zone zone)
        {
            if (_core?.ZoneManager == null || player == null) return null;
            try
            {
                return _core.ZoneManager.GetCards(player, zone);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        // ======================================== 查询（选择/表现层的映射 API） ========================================

        /// <summary>格上的卡：单位/地牌格返回占用者；发动格返回叠放顶（最后一张 = 最新发动）</summary>
        public Card CardAt(int x, int z)
        {
            if (!BoardMath.InBounds(x, z)) return null;
            int idx = BoardMath.Index(x, z);

            if (BoardLayout.RoleOf(x, z) == CellRole.Activation)
            {
                var pile = BoardLayout.OwnerOf(x, z) == 0 ? _activation0 : _activation1;
                return pile.Count > 0 ? pile[pile.Count - 1] : null;
            }

            return _occupant.TryGetValue(idx, out var card) ? card : null;
        }

        /// <summary>卡所在格（未落格 = false；发动区中的卡不在 _cellOf，用 ActivationIndexOf 查叠放位次）</summary>
        public bool TryGetCell(Card card, out int x, out int z)
        {
            x = z = -1;
            if (card == null || !_cellOf.TryGetValue(card, out int idx)) return false;
            x = idx % BoardMath.Width;
            z = idx / BoardMath.Width;
            return true;
        }

        /// <summary>玩家发动格叠放（底部 = 最早发动；Phase A 通常为空）</summary>
        public IReadOnlyList<Card> ActivationPile(Player player)
        {
            return PlayerIndex(player) == 0 ? (IReadOnlyList<Card>)_activation0 : _activation1;
        }

        /// <summary>玩家角色格坐标（核心不存坐标——这就是布局常量）</summary>
        public (int x, int z) CharacterCell(Player player) => BoardLayout.CharacterCell(PlayerIndex(player));

        public int PlayerIndex(Player player) => player == _p2 ? 1 : 0;

        // ======================================== 战棋预留（距离公式，无玩法接线） ========================================

        /// <summary>攻击距离 = 六距 + (Tactical ? |落差| : 0)。Flat 档落差恒视为 0（P2P 完全平面）</summary>
        public int AttackDistance(int x1, int z1, int x2, int z2)
        {
            int dist = BoardMath.HexDistance(x1, z1, x2, z2);
            if (Mode == BoardRulesMode.Tactical)
                dist += Math.Abs(Field.ElevationAt(x1, z1) - Field.ElevationAt(x2, z2));
            return dist;
        }

        /// <summary>两张在场卡是否在攻击距离内（未落格 = false）</summary>
        public bool InAttackRange(Card a, Card b, int range = DefaultAttackRange)
        {
            if (!TryGetCell(a, out int ax, out int az) || !TryGetCell(b, out int bx, out int bz))
                return false;
            return AttackDistance(ax, az, bx, bz) <= range;
        }

        // ======================================== 一致性自检（验证器用） ========================================

        /// <summary>占用双向索引是否成对一致、无悬挂（TestBoard 的核心断言之一）</summary>
        public bool IsConsistent()
        {
            if (_occupant.Count != _cellOf.Count) return false;
            foreach (var kv in _occupant)
            {
                if (!_cellOf.TryGetValue(kv.Value, out int idx) || idx != kv.Key) return false;
            }
            return true;
        }

        // ======================================== 自动刷新（可选） ========================================

        /// <summary>
        /// 订阅核心事件自动 Resync（幂等全量重建，事件多刷无害）。
        /// 验证器/确定性断言场景不启用，手动调 Resync。
        /// </summary>
        public void EnableAutoResync()
        {
            if (_autoResync) return;
            _autoResync = true;

            void Hook<T>() where T : class, IGameEvent
            {
                Action<T> handler = _ => Resync();
                EventManager.Instance.Subscribe(handler);
                _subscriptions.Add(new Subscription<T>(handler));
            }

            Hook<CardPutToBattlefieldEvent>();
            Hook<CardZoneChangeEvent>();
            Hook<CardEnterActivationEvent>();
            Hook<CardLeaveActivationEvent>();
            Hook<CardActivationFailedEvent>();
            Hook<ElementPoolAddEvent>();
            Hook<ElementPoolDepleteEvent>();
            Hook<TokenCreatedEvent>();

            Resync();
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();
            _autoResync = false;
        }

        /// <summary>延迟 Unsubscribe 的小包装（存 handler 供释放）</summary>
        private sealed class Subscription<T> : IDisposable where T : class, IGameEvent
        {
            private readonly Action<T> _handler;
            public Subscription(Action<T> handler) => _handler = handler;
            public void Dispose() => EventManager.Instance.Unsubscribe(_handler);
        }
    }
}
