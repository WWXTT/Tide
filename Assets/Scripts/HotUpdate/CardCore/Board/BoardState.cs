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
    /// - 占用分配确定性：钉优先→上轮格粘滞（2026-10-16 死亡不挪位：离场只让出自己的格，
    ///   其余单位不顺移补空）→first-free；Resync 幂等，连续两次结果逐格一致；全新实例
    ///   无上轮落位=纯 first-free，重连/late-join 零字节重建不受影响；
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
        private readonly Dictionary<int, Card> _occupant = new Dictionary<int, Card>(); // cellIndex -> 卡（单位/地牌色组首张）
        private readonly Dictionary<Card, int> _cellOf = new Dictionary<Card, int>();   // 反向（地牌同色组共享同格）

        // 落位钉子（2026-10-06 教学直入）：卡 → 指定格 index。Resync 时带钉卡优先占钉格
        // （钉格须属其所在区的格集，否则按无钉顺延），其余卡先回上轮格（位置粘滞）再填空。
        // 卡离场（不再落格）钉子在 Resync 尾自动清除。
        private readonly Dictionary<Card, int> _pinnedCells = new Dictionary<Card, int>();

        // 施放占格（2026-10-15 发动格拆除定案：施放中的卡=发动区占位期，落在该玩家单位区空格上
        // 呈现——整个单位区兼任发动区（P11 后 4+5=9 格）；永久物=玩家所选钉格原地转正，
        // 法术/响应施放=first-free）。
        // 与战场占用**分离存放**：施放卡不进 _cellOf（TryGetCell=false——光环 TryGetCellOf、
        // 攻击距离、邻接查询不视其为战场单位），CardAt 合并可见（出牌选格/呈现层需要占用真值）。
        // Enter/Leave/Failed 事件钩子已引全量 Resync，本表由 Resync 纯重建（钉优先→first-free）。
        private readonly Dictionary<Card, int> _castingCellOf = new Dictionary<Card, int>();   // 施放卡 -> 格 index
        private readonly Dictionary<int, Card> _castingOccupant = new Dictionary<int, Card>(); // 格 index -> 施放卡

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
        /// 战场列表 → 己方单位区格序 钉优先→上轮格粘滞→first-free；元素池列表 → 地牌行格序；
        /// 发动区列表 → 单位区空格施放占格（钉优先→上轮格粘滞→first-free，2026-10-15 发动格拆除）。
        /// 容量外（控制权迁移等边缘）不落格、不抛错。
        /// </summary>
        public void Resync()
        {
            // 上一轮落位快照（位置粘滞用）：先留档再清表——无钉单位优先回自己上轮的格
            var prevCells = _cellOf.Count > 0 ? new Dictionary<Card, int>(_cellOf) : null;
            var prevCasting = _castingCellOf.Count > 0 ? new Dictionary<Card, int>(_castingCellOf) : null;

            _occupant.Clear();
            _cellOf.Clear();
            _castingCellOf.Clear();
            _castingOccupant.Clear();

            AssignCells(_p1, Zone.Battlefield, BoardLayout.UnitCells(0), prevCells);
            AssignCells(_p2, Zone.Battlefield, BoardLayout.UnitCells(1), prevCells);
            AssignCells(_p1, Zone.ElementPool, BoardLayout.LandCells(0));
            AssignCells(_p2, Zone.ElementPool, BoardLayout.LandCells(1));

            PlaceCasting(_p1, 0, prevCasting);
            PlaceCasting(_p2, 1, prevCasting);

            // 落位钉子清理：本轮未落格的卡（离场/换区）钉子不保留（施放占格期保钉——入场要用）
            if (_pinnedCells.Count > 0)
            {
                var stale = new List<Card>();
                foreach (var card in _pinnedCells.Keys)
                    if (!_cellOf.ContainsKey(card) && !_castingCellOf.ContainsKey(card)) stale.Add(card);
                foreach (var card in stale) _pinnedCells.Remove(card);
            }
        }

        private void AssignCells(Player player, Zone zone, IReadOnlyList<(int x, int z)> cells,
            Dictionary<Card, int> prev = null)
        {
            // 地牌走按色叠格（2026-10-14 定案：同色叠一格，颜色按首现序占格）——独立路径
            if (zone == Zone.ElementPool) { AssignLandCells(player, cells); return; }

            var cards = SafeGetCards(player, zone);
            if (cards == null) return;

            // 场地卡（2026-10-29 P8）：战场批先摘出——固定落己方场地格（BoardLayout.FieldCell 单源），
            // 不进单位区分配（钉子/粘滞/first-free 全不适用）。正常流每方至多一张（顶替先旧后新，
            // Zones 入场口）；异常多张（效果直加等）=首张占格、其余容量外告警不落格。
            if (zone == Zone.Battlefield)
            {
                bool anyField = false;
                foreach (var c in cards)
                    if (c != null && c.IsFieldCard()) { anyField = true; break; }
                if (anyField)
                {
                    Card firstField = null;
                    var nonField = new List<Card>();
                    foreach (var c in cards)
                    {
                        if (c == null || !c.IsFieldCard()) { nonField.Add(c); continue; }
                        if (firstField == null) firstField = c;
                        else TideLog.Warn($"[BoardState] 场地卡 {c.ID} 容量外未落格（每方一格，已有 {firstField.ID} 在格）");
                    }
                    var fc = BoardLayout.FieldCell(PlayerIndex(player));
                    int fIdx = BoardMath.Index(fc.x, fc.z);
                    _occupant[fIdx] = firstField;
                    _cellOf[firstField] = fIdx;
                    cards = nonField;
                    if (cards.Count == 0) return; // 本批全是场地卡：单位区分配无事可做
                }
            }

            // 落位钉子优先：本批带合法钉的卡占住钉格；其余卡先回自己上轮的格（位置粘滞，
            // 2026-10-16 死亡不挪位：同批死亡/离场只让出自己的格，其余单位不再顺移补空），
            // 回不上（新入场/格被钉占/换半场）才按列表序填剩余空格。
            // 施放占格避让说明（2026-10-15）：本方法先于 PlaceCasting 跑（战场批先落，
            // 施放字典此刻恒空）——单位不感知施放卡；极窄口（施放窗内新单位入场占施放格）由
            // 「施放卡挪格呈现 + 转正时钉优先拿回自己的格、其余单位重排」自愈收敛。
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

            var usage = new HashSet<int>(pinned.Keys);
            // 两轮分配：先回座（位置粘滞），后 first-free——新入场者不挤已就座者
            var reseated = new HashSet<Card>();
            foreach (var card in unpinned)
            {
                if (prev != null && prev.TryGetValue(card, out int old)
                    && cellIndex.Contains(old) && !usage.Contains(old))
                {
                    usage.Add(old);
                    _occupant[old] = card;
                    _cellOf[card] = old;
                    reseated.Add(card);
                }
            }
            foreach (var card in unpinned)
            {
                if (reseated.Contains(card)) continue;
                int placed = -1;
                foreach (var c in cells)
                {
                    int idx = BoardMath.Index(c.x, c.z);
                    if (!usage.Contains(idx)) { placed = idx; break; }
                }
                if (placed < 0)
                {
                    // 2026-10-21 段零召唤物无视觉取证：容量外不落格原口径静默——埋点显形
                    TideLog.Warn($"[BoardState] {card?.ID} 容量外未落格（本区格数={cells.Count} 已占={usage.Count}）");
                    break;
                }
                usage.Add(placed);
                _occupant[placed] = card;
                _cellOf[card] = placed;
            }
            foreach (var kv in pinned)
            {
                _occupant[kv.Key] = kv.Value;
                _cellOf[kv.Value] = kv.Key;
            }
        }

        /// <summary>地牌固定元素格（2026-10-14 晚定案·2026-10-28 元素缩编）：第 i 格元素身份=
        /// BoardLayout.LandCellElementOrder[i]（灰红蓝绿）——颜色直接落自己的格（同色全部叠进同一格）。
        /// 无锁色映射的异常卡按灰聚拢。旧「首现序占格+地牌钉优先」随定案退役：固定映射天然稳定
        ///（Resync 幂等），_pinnedCells 只剩单位格路径消费。
        /// 旧「每格两层叠放」（2026-10-09）更早已退役：IsStackedLand/_stackOccupant 已删。</summary>
        private void AssignLandCells(Player player, IReadOnlyList<(int x, int z)> cells)
        {
            var cards = SafeGetCards(player, Zone.ElementPool);
            if (cards == null) return;

            // 锁色映射（池包装权威，卡上无地牌字段）：SourceCard -> LockedColor
            var lockedOf = new Dictionary<Card, ManaType>();
            var pooled = _core?.ElementPool?.GetPooledCards(player);
            if (pooled != null)
                foreach (var pc in pooled)
                    if (pc?.SourceCard != null) lockedOf[pc.SourceCard] = pc.LockedColor;

            // 按色分组（无锁色映射的异常卡按灰聚拢）→ 各落自己的固定元素格（BoardLayout 单源）
            int pIdx = PlayerIndex(player);
            var byColor = new Dictionary<ManaType, List<Card>>();
            foreach (var card in cards)
            {
                if (card == null) continue;
                var color = lockedOf.TryGetValue(card, out var locked) ? locked : ManaType.Gray;
                if (!byColor.TryGetValue(color, out var list))
                {
                    list = new List<Card>();
                    byColor[color] = list;
                }
                list.Add(card);
            }
            foreach (var kv in byColor)
            {
                var cell = BoardLayout.LandCellOfElement(pIdx, kv.Key);
                if (cell.x < 0) break; // 地牌行格集为空（防御）
                int idx = BoardMath.Index(cell.x, cell.z);
                _occupant[idx] = kv.Value[0];
                foreach (var card in kv.Value) _cellOf[card] = idx;
            }
        }

        /// <summary>地牌落格预览（2026-10-14 箭头放地）：假想 newColor 此刻入池的落格——
        /// 固定元素格映射即答案（BoardLayout.LandCellOfElement 单源，与 AssignLandCells 同源恒一致），
        /// 供 UI 放地箭头高亮"该色将落的格"；正式落格仍以入池后 Resync 的 AssignLandCells 为准。</summary>
        public (int x, int z)? PreviewLandCell(Player player, ManaType newColor)
        {
            var cells = BoardLayout.LandCells(PlayerIndex(player));
            if (cells.Count == 0) return null;
            var cell = BoardLayout.LandCellOfElement(PlayerIndex(player), newColor);
            return cell.x < 0 ? null : ((int x, int z)?)cell;
        }

        // ---- 场地格查询（2026-10-29 P8：每方一格·自顶替）----
        // 直读战场区列表（Resync 前亦可用）：该玩家战场区中的场地卡即格上卡——
        // 落位权威=AssignCells 的 FieldCell 分支，两处同源恒一致。

        /// <summary>玩家的场地卡（己方场地格上的占用者；无场地=null）。异常多张取首张。</summary>
        public Card FieldCardOf(Player player)
        {
            var cards = SafeGetCards(player, Zone.Battlefield);
            if (cards == null) return null;
            foreach (var c in cards)
                if (c != null && c.IsFieldCard()) return c;
            return null;
        }

        /// <summary>己方场地格是否已有场地卡（P8：每方一张——新场地打出=顶替）。</summary>
        public bool FieldOccupied(Player player) => FieldCardOf(player) != null;

        /// <summary>教学落位钉子（2026-10-06 教学直入）：钉住一张卡的 Resync 落位格——
        /// 该卡之后每次重建优先回到钉格（须属其所在区格集，否则按无钉顺延）；卡离场钉子
        /// 自动清除。供 TutorialScenarioSeeder 预设场面、出牌选格（声明成功即钉）用。</summary>
        public void PinCard(Card card, int x, int z)
        {
            if (card == null || !BoardMath.InBounds(x, z)) return;
            _pinnedCells[card] = BoardMath.Index(x, z);
            Resync();
        }

        /// <summary>
        /// 施放占格（2026-10-15 发动格拆除）：发动区列表 → 该玩家单位区空格。
        /// 钉优先（永久物出牌=玩家所选格，钉格须属己方单位区且未被战场/其他施放卡占用），
        /// 其余（法术/响应施放）按单位区格序 first-free；无空格不落格不抛错（立牌不显示）。
        /// </summary>
        private void PlaceCasting(Player player, int pIdx, Dictionary<Card, int> prev = null)
        {
            var cards = SafeGetCards(player, Zone.Activation);
            if (cards == null) return;

            var cells = BoardLayout.UnitCells(pIdx);
            var cellIndex = new HashSet<int>();
            foreach (var c in cells) cellIndex.Add(BoardMath.Index(c.x, c.z));

            foreach (var card in cards)
            {
                if (card == null) continue;
                // 场地卡（2026-10-29 P8）：施放立牌恒在己方场地格（与入场落位同格）——旧场地
                // 仍占格时不立牌（顶替在 cast 结算才发生，防同格双卡重叠），空则落格预告位。
                if (card.IsFieldCard())
                {
                    var fc = BoardLayout.FieldCell(pIdx);
                    int fIdx = BoardMath.Index(fc.x, fc.z);
                    if (!_occupant.ContainsKey(fIdx) && !_castingOccupant.ContainsKey(fIdx))
                    {
                        _castingCellOf[card] = fIdx;
                        _castingOccupant[fIdx] = card;
                    }
                    continue;
                }
                int placed = -1;
                if (_pinnedCells.TryGetValue(card, out int pin)
                    && cellIndex.Contains(pin)
                    && !_occupant.ContainsKey(pin) && !_castingOccupant.ContainsKey(pin))
                {
                    placed = pin;
                }
                else if (prev != null && prev.TryGetValue(card, out int old)
                    && cellIndex.Contains(old)
                    && !_occupant.ContainsKey(old) && !_castingOccupant.ContainsKey(old))
                {
                    placed = old; // 位置粘滞：施放立牌回上轮格，无关重排（如死亡）不再挪动它
                }
                else
                {
                    foreach (var c in cells)
                    {
                        int idx = BoardMath.Index(c.x, c.z);
                        if (_occupant.ContainsKey(idx) || _castingOccupant.ContainsKey(idx)) continue;
                        placed = idx; break;
                    }
                }
                if (placed < 0) continue;
                _castingCellOf[card] = placed;
                _castingOccupant[placed] = card;
            }
        }

        /// <summary>施放占格快照（呈现层数据道）：发动区占位卡 × 落格 × 归属；未落格卡不列。</summary>
        public List<(Card card, int x, int z, int owner)> CastingPlacements()
        {
            var list = new List<(Card, int, int, int)>();
            AppendCasting(_p1, 0, list);
            AppendCasting(_p2, 1, list);
            return list;
        }

        private void AppendCasting(Player player, int owner, List<(Card card, int x, int z, int ownerIdx)> list)
        {
            var cards = SafeGetCards(player, Zone.Activation);
            if (cards == null) return;
            foreach (var card in cards)
                if (card != null && _castingCellOf.TryGetValue(card, out int idx))
                    list.Add((card, idx % BoardMath.Width, idx / BoardMath.Width, owner));
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

        /// <summary>格上的卡：单位/地牌格返回占用者；施放占格（发动区占位卡）次优先返回施放卡
        ///（占用真值——出牌选格/呈现层用；战斗邻接走 Neighbors/FlankNeighbors 已滤施放卡）</summary>
        public Card CardAt(int x, int z)
        {
            if (!BoardMath.InBounds(x, z)) return null;
            int idx = BoardMath.Index(x, z);
            if (_occupant.TryGetValue(idx, out var card)) return card;
            return _castingOccupant.TryGetValue(idx, out var casting) ? casting : null;
        }

        /// <summary>卡所在格（未落格 = false；施放占位卡不在 _cellOf——战场/光环/距离口径
        /// 均不视其为在场单位，落格查询走 TryGetCastingCell）</summary>
        public bool TryGetCell(Card card, out int x, out int z)
        {
            x = z = -1;
            if (card == null || !_cellOf.TryGetValue(card, out int idx)) return false;
            x = idx % BoardMath.Width;
            z = idx / BoardMath.Width;
            return true;
        }

        /// <summary>施放占位卡落格查询（呈现层）：发动区占位卡 → 单位区格；未落格 = false。</summary>
        public bool TryGetCastingCell(Card card, out int x, out int z)
        {
            x = z = -1;
            if (card == null || !_castingCellOf.TryGetValue(card, out int idx)) return false;
            x = idx % BoardMath.Width;
            z = idx / BoardMath.Width;
            return true;
        }

        /// <summary>卡是否处于施放占格期（发动区占位）——邻接/战斗查询过滤用。</summary>
        public bool IsCasting(Card card) => card != null && _castingCellOf.ContainsKey(card);

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

        /// <summary>卡所在格的 6 邻格占用卡（通用邻接查询；未落格 = 空枚举）。
        /// 施放占位卡不算在场单位（2026-10-15 发动格拆除：施放卡只是占格呈现）——滤除。</summary>
        public IEnumerable<Card> Neighbors(Card card)
        {
            if (!TryGetCell(card, out int x, out int z)) yield break;
            foreach (BoardDirection dir in Enum.GetValues(typeof(BoardDirection)))
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                var neighbor = CardAt(nx, nz);
                if (neighbor != null && neighbor != card && !IsCasting(neighbor))
                    yield return neighbor;
            }
        }

        /// <summary>团结邻接计数（2026-10-20 团结引擎·BranchEngines.UnityAdjacentCountOf 注入）：
        /// 此卡 6 邻格中的"单位"数——生物/结界占格各 1（单位区占用者）、角色格恒 1（角色无卡实体按格计）、
        /// 非空地牌格 1（同色组首张占格=非空；空地牌格无占用不计）；施放占位卡非在场单位不计
        ///（战斗邻接同口径）；自身不计；未落格 = 0；六邻天然封顶 6。</summary>
        public int AdjacentOccupantCount(Card card)
        {
            if (!TryGetCell(card, out int x, out int z)) return 0;
            int count = 0;
            foreach (BoardDirection dir in Enum.GetValues(typeof(BoardDirection)))
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                if (!BoardMath.InBounds(nx, nz)) continue;
                if (BoardLayout.RoleOf(nx, nz) == CellRole.Character) { count++; continue; }
                var occupant = CardAt(nx, nz);
                if (occupant != null && occupant != card && !IsCasting(occupant)) count++;
            }
            return count;
        }

        /// <summary>卡所在格左右同排（E/W 两向）邻格占用生物（碾压 AdjacentResolver 接线用；
        /// 未落格 = 空枚举）。2026-10-04 碾压语义修订：溅射只打目标左右两侧生物——
        /// 不再吃六邻格（后排斜邻/贴边地牌行的地牌不入溅射域）。施放占位卡非在场生物——滤除。</summary>
        public IEnumerable<Card> FlankNeighbors(Card card)
        {
            if (!TryGetCell(card, out int x, out int z)) yield break;
            foreach (var dir in new[] { BoardDirection.E, BoardDirection.W })
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                var neighbor = CardAt(nx, nz);
                if (neighbor != null && neighbor != card && !IsCasting(neighbor))
                    yield return neighbor;
            }
        }

        /// <summary>角色格左右同排（E/W 两向）邻格占用生物（碾压 RoleAdjacentResolver 接线用，
        /// 2026-10-16 碾压攻角色改版：溅射域=角色格左右邻格生物）。8×7 布局下角色格居左右列端头
        ///（P1=(0,1)/P2=(7,5)）——E/W 仅一格在盘（对方侧出界自然跳过）。施放占位卡非在场生物——滤除。</summary>
        public IEnumerable<Card> FlankNeighborsOfRole(Player player)
        {
            var (x, z) = CharacterCell(player);
            foreach (var dir in new[] { BoardDirection.E, BoardDirection.W })
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                var neighbor = CardAt(nx, nz);
                if (neighbor != null && !IsCasting(neighbor))
                    yield return neighbor;
            }
        }

        // ======================================== 一致性自检（验证器用） ========================================

        /// <summary>占用双向索引是否成对一致、无悬挂（TestBoard 的核心断言之一；
        /// 地牌同色组多卡共享一格——正向表按格计，反向表每张卡都须指向有占用者的格。
        /// 施放占格同式成对校验，且与战场占用不得同格重叠）</summary>
        public bool IsConsistent()
        {
            foreach (var kv in _occupant)
            {
                if (!_cellOf.TryGetValue(kv.Value, out int idx) || idx != kv.Key) return false;
            }
            foreach (var kv in _cellOf)
            {
                if (!_occupant.ContainsKey(kv.Value)) return false;
            }
            foreach (var kv in _castingCellOf)
            {
                if (!_castingOccupant.TryGetValue(kv.Value, out var c) || !ReferenceEquals(c, kv.Key)) return false;
                if (_occupant.ContainsKey(kv.Value)) return false;
            }
            foreach (var kv in _castingOccupant)
            {
                if (!_castingCellOf.TryGetValue(kv.Value, out int idx) || idx != kv.Key) return false;
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
            // ElementPoolDepleteEvent 已删（2026-10-11 指示物/耗竭退役）——地牌离池改由
            // 摧毁/抹除 handler 发 CardDestroyEvent/CardMoveEvent，AutoResync 经常规钩子同步
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

    /// <summary>
    /// 移动原子规则单源（2026-10-31 场面轴3c MoveUnit）：合法性判定与执行。
    /// 执行=PinCard 钉格 + Resync 幂等全量重建——占用/邻接（碾压 FlankNeighbors）/箭头连接光环
    /// （LinkAuraSystem 活查）/攻击距离全部按新格即时派生；**不新增坐标系统、不写新状态字段**
    /// （Card 零坐标字段的架构铁律不变）。
    /// 装配同 LinkAuraSystem 范式：宿主（BattleController.AttachBoard）Attach；未装配=无棋盘
    /// 环境（服务器早期/纯核心测试），TryMove 恒 false、LegalDestinations 恒空。
    /// 联机 TODO（2026-10-31）：联机快照不承载坐标，本阶段只保证单机/本地对局——协议承载
    /// 选格参数后由双端各自 Apply，NetRoom 暂不 Attach 本类（移动在联机局恒走空转支路）。
    /// </summary>
    public static class MoveUnitRules
    {
        private static BoardState _board;

        /// <summary>宿主装配（AttachBoard 调；幂等换挂）。</summary>
        public static void Attach(BoardState board) => _board = board;

        /// <summary>宿主卸载（DetachBoard 调——静态不清理会指向过期占用层，S5 口径）。</summary>
        public static void Detach() => _board = null;

        /// <summary>单位归属半场格集判定的属主（控制者优先、回落拥有者——与 AddCounters 咽喉同口径）。</summary>
        private static Player OwnerOf(Card unit) => unit?.GetController() ?? unit?.GetOwner();

        /// <summary>
        /// 合法落格：该单位所在方单位区内 · 空格（无单位/无施放占位——CardAt 占用真值）· 非原地。
        /// 未装配/无属主/未落格单位照常返回其半场空格（未落格无原地排除）。
        /// </summary>
        public static List<(int x, int z)> LegalDestinations(Card unit)
        {
            var result = new List<(int x, int z)>();
            var board = _board;
            var owner = OwnerOf(unit);
            if (board == null || owner == null) return result;
            board.TryGetCell(unit, out int ux, out int uz);
            foreach (var cell in BoardLayout.UnitCells(board.PlayerIndex(owner)))
            {
                if (cell.x == ux && cell.z == uz) continue;         // 原地不可
                if (board.CardAt(cell.x, cell.z) != null) continue; // 占用真值（含施放占格卡）
                result.Add((cell.x, cell.z));
            }
            return result;
        }

        /// <summary>
        /// 执行移动：终检（己方单位区格集内 · 空格 · 非原地）后 PinCard（内含 Resync）。
        /// 通过返回 true；任何不合法返回 false 不动盘面。
        /// </summary>
        public static bool TryMove(Card unit, int x, int z)
        {
            var board = _board;
            var owner = OwnerOf(unit);
            if (board == null || owner == null || !BoardMath.InBounds(x, z)) return false;
            if (board.TryGetCell(unit, out int ux, out int uz) && ux == x && uz == z) return false; // 原地
            bool inZone = false;
            foreach (var c in BoardLayout.UnitCells(board.PlayerIndex(owner)))
                if (c.x == x && c.z == z) { inZone = true; break; }
            if (!inZone) return false;                              // 须属该方单位区
            if (board.CardAt(x, z) != null) return false;           // 空格（含施放占位）
            board.PinCard(unit, x, z);
            return true;
        }
    }
}
