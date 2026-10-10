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
        private readonly Dictionary<int, Card> _occupant = new Dictionary<int, Card>(); // cellIndex -> 卡（单位/地牌首层）
        private readonly Dictionary<Card, int> _cellOf = new Dictionary<Card, int>();   // 反向

        // 地牌叠层（2026-10-09 地牌槽提升·每格两张）：cellIndex -> 第二层地牌。
        // 仅地牌行（Zone.ElementPool）使用；两层共用 _cellOf 反查（同格坐标），
        // 视图层经 IsStackedLand 区分错位渲染。单位行恒一层，不入此表。
        private readonly Dictionary<int, Card> _stackOccupant = new Dictionary<int, Card>();

        // 落位钉子（2026-10-06 教学直入）：卡 → 指定格 index。Resync 时带钉卡优先占钉格
        // （钉格须属其所在区的格集，否则按无钉顺延），其余卡按区域列表序填空——无人调钉时
        // 分配与原「列表序位」逐位一致。卡离场（不再落格）钉子在 Resync 尾自动清除。
        private readonly Dictionary<Card, int> _pinnedCells = new Dictionary<Card, int>();

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
            _stackOccupant.Clear();
            _activation0.Clear();
            _activation1.Clear();

            AssignCells(_p1, Zone.Battlefield, BoardLayout.UnitCells(0));
            AssignCells(_p2, Zone.Battlefield, BoardLayout.UnitCells(1));
            AssignCells(_p1, Zone.ElementPool, BoardLayout.LandCells(0));
            AssignCells(_p2, Zone.ElementPool, BoardLayout.LandCells(1));

            AppendActivation(_p1, _activation0);
            AppendActivation(_p2, _activation1);

            // 落位钉子清理：本轮未落格的卡（离场/换区）钉子不保留
            if (_pinnedCells.Count > 0)
            {
                var stale = new List<Card>();
                foreach (var card in _pinnedCells.Keys)
                    if (!_cellOf.ContainsKey(card)) stale.Add(card);
                foreach (var card in stale) _pinnedCells.Remove(card);
            }
        }

        private void AssignCells(Player player, Zone zone, IReadOnlyList<(int x, int z)> cells)
        {
            var cards = SafeGetCards(player, zone);
            if (cards == null) return;

            // 落位钉子优先：本批带合法钉的卡占住钉格；其余卡按列表序填剩余空格（原口径）
            var cellIndex = new HashSet<int>();
            foreach (var c in cells) cellIndex.Add(BoardMath.Index(c.x, c.z));

            var pinned = new Dictionary<int, Card>(); // cellIdx -> 卡（同批先到先得）
            var unpinned = new List<Card>();
            foreach (var card in cards)
            {
                if (_pinnedCells.TryGetValue(card, out int idx)
                    && cellIndex.Contains(idx) && !pinned.ContainsKey(idx))
                    pinned[idx] = card;
                else
                    unpinned.Add(card);
            }

            // 每格容量（2026-10-09 地牌槽提升·叠放定案）：地牌行每格两张（第 10~18 张地牌
            // 叠入已有格第二层，上限 18=9格×2 与 ElementPool.MaxStackedLandCap 对应）；
            // 其余行恒 1（行为与原 free 队列逐位一致）。usage 预计钉子占层——叠层可叠上钉格。
            int capacity = zone == Zone.ElementPool ? BoardLayout.LandStackPerCell : 1;
            var usage = new Dictionary<int, int>();
            foreach (var kv in pinned) usage[kv.Key] = 1;

            foreach (var card in unpinned)
            {
                // 铺满再叠（2026-10-09 修正）：首层空位优先——先铺满整行（与 BattleViewData.FillLandsNet
                // i%9 同格、服务器 AssignCells 同构），无空位才叠第二层；原「顺格贪心叠两连张」错位。
                int empty = -1, stackable = -1;
                foreach (var c in cells)
                {
                    int idx = BoardMath.Index(c.x, c.z);
                    usage.TryGetValue(idx, out int used);
                    if (used == 0) { empty = idx; break; }
                    if (used < capacity && stackable < 0) stackable = idx;
                }
                int placed = empty >= 0 ? empty : stackable;
                if (placed < 0) break; // 容量外（控制权迁移等边缘）不落格、不抛错（原口径）
                usage.TryGetValue(placed, out int layer);
                if (layer == 0) _occupant[placed] = card;
                else _stackOccupant[placed] = card;
                usage[placed] = layer + 1;
                _cellOf[card] = placed;
            }
            foreach (var kv in pinned)
            {
                _occupant[kv.Key] = kv.Value;
                _cellOf[kv.Value] = kv.Key;
            }
        }

        /// <summary>地牌叠层查询（2026-10-09 地牌槽提升）：卡是否为所在格的第二层（叠放张）——
        /// 视图层据此对叠放张错位渲染。未落格/首层/单位行恒 false。</summary>
        public bool IsStackedLand(Card card)
            => card != null && _cellOf.TryGetValue(card, out int idx)
               && _stackOccupant.TryGetValue(idx, out var top) && top == card;

        /// <summary>教学落位钉子（2026-10-06 教学直入）：钉住一张卡的 Resync 落位格——
        /// 该卡之后每次重建优先回到钉格（须属其所在区格集，否则按无钉顺延），其余卡填空；
        /// 卡离场钉子自动清除。供 TutorialScenarioSeeder 预设场面用；正常对局无人调钉，行为不变。</summary>
        public void PinCard(Card card, int x, int z)
        {
            if (card == null || !BoardMath.InBounds(x, z)) return;
            _pinnedCells[card] = BoardMath.Index(x, z);
            Resync();
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

        /// <summary>卡所在格的 6 邻格占用卡（通用邻接查询；未落格 = 空枚举）</summary>
        public IEnumerable<Card> Neighbors(Card card)
        {
            if (!TryGetCell(card, out int x, out int z)) yield break;
            foreach (BoardDirection dir in Enum.GetValues(typeof(BoardDirection)))
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                var neighbor = CardAt(nx, nz);
                if (neighbor != null && neighbor != card)
                    yield return neighbor;
            }
        }

        /// <summary>卡所在格左右同排（E/W 两向）邻格占用生物（碾压 AdjacentResolver 接线用；
        /// 未落格 = 空枚举）。2026-10-04 碾压语义修订：溅射只打目标左右两侧生物——
        /// 不再吃六邻格（后排斜邻/贴边地牌行的地牌不入溅射域）。</summary>
        public IEnumerable<Card> FlankNeighbors(Card card)
        {
            if (!TryGetCell(card, out int x, out int z)) yield break;
            foreach (var dir in new[] { BoardDirection.E, BoardDirection.W })
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                var neighbor = CardAt(nx, nz);
                if (neighbor != null && neighbor != card)
                    yield return neighbor;
            }
        }

        // ======================================== 一致性自检（验证器用） ========================================

        /// <summary>占用双向索引是否成对一致、无悬挂（TestBoard 的核心断言之一）</summary>
        public bool IsConsistent()
        {
            // 叠层表计入（2026-10-09 地牌叠放）：首层+第二层卡数合计与反向索引对账
            if (_occupant.Count + _stackOccupant.Count != _cellOf.Count) return false;
            foreach (var kv in _occupant)
            {
                if (!_cellOf.TryGetValue(kv.Value, out int idx) || idx != kv.Key) return false;
            }
            foreach (var kv in _stackOccupant)
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
            Hook<CardLeaveBattlefieldEvent>(); // 战场离场（死亡/放逐/回手）同步事件——容器层直发；
            // 2026-09-26 补钩（BattlefieldVerifier S2 发现）：漏钩时离场只能等 SBA 快照扫描
            // 补发 CardZoneChangeEvent 才追上，窗口期内棋盘留幽灵占用（HUD/碾压邻接过期）
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
