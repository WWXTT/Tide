using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;

namespace GameBoard
{
    /// <summary>
    /// 连接光环运行时（三轨制定案 2026-09-09）——live-query 直查 + 版本号脏缓存。
    ///
    /// 语义：带箭头生物 → 箭头指向的那一格的**当前占据者**享受其卡面 LinkAuras 声明
    /// （单向指向，无「相互连接/共享」概念；箭头不指向自己）。方向随玩家视角镜像：
    /// 布局归属 1（上半场）的箭头绝对方向取 Opposite（BoardMath.MapArrow/Opposite）。
    ///
    /// 失效口径（live-query 天然成立，无物化状态可失联）：
    /// - 来源须持续在场：离场/未落格自然无贡献（断链即失效）；
    /// - 来源被「无效」指示物压制（唯一能压光环的口——净化/沉默不压箭头：箭头是卡面数据）；
    /// - 受益者不可被干扰：光环不物化、不入 _keywords/_counters——净化/沉默/无效打在受益者
    ///   身上都不能影响光环（读数经 EntityEffectExtensions.GetPower/GetLife/HasKeyword 并入）。
    ///
    /// 接线（CombatSystem.AdjacentResolver 同惯例）：组合根 Attach(BoardState) 注入占用查询
    /// 并订阅失效事件；未注入（纯核心测试/headless 无棋盘）时 Enabled=false，全部查询 O(1) 早退=无光环。
    /// 计价按单回合指示物档（来源须持续在场的折价，见 CardCostService.ComputeLinkAuraBuckets）。
    /// </summary>
    public static class LinkAuraSystem
    {
        // —— 组合根注入的占用查询（核心不绑棋盘：静态委托扩展点）——
        public static Func<Card, (int x, int z)?> TryGetCellOf { get; private set; }
        public static Func<int, int, Card> CardAt { get; private set; }
        public static Func<Card, int> OwnerIndexOf { get; private set; }

        /// <summary>是否已注入棋盘（未注入 = 无光环，所有查询 O(1) 早退）</summary>
        public static bool Enabled => TryGetCellOf != null && CardAt != null && OwnerIndexOf != null;

        private static int _version; // 失效版本号（事件驱动 +1；缓存按版本号判脏）
        private static readonly Dictionary<Card, CachedBonus> _cache = new Dictionary<Card, CachedBonus>();
        private static readonly List<IDisposable> _subs = new List<IDisposable>();

        private sealed class CachedBonus
        {
            public int Version;
            public int Power;
            public int Life;
            public readonly List<string> Keywords = new List<string>();
        }

        private static readonly HexDirection[] ArrowBits =
        {
            HexDirection.Up, HexDirection.UpperRight, HexDirection.LowerRight,
            HexDirection.Down, HexDirection.LowerLeft, HexDirection.UpperLeft,
        };

        /// <summary>
        /// 组合根接线（幂等）：注入占用三委托 + 订阅失效事件（占用/离场/无效增减均触发失效）。
        /// </summary>
        public static void Attach(BoardState board)
        {
            if (board == null || Enabled) return;

            (int x, int z)? CellOf(Card c)
            {
                return board.TryGetCell(c, out int x, out int z) ? (x, z) : default;
            }
            TryGetCellOf = CellOf;
            CardAt = board.CardAt;
            OwnerIndexOf = card =>
            {
                var controller = card?.GetController();
                return controller == null ? -1 : board.PlayerIndex(controller);
            };

            Hook<CardPutToBattlefieldEvent>();
            Hook<CardLeaveBattlefieldEvent>();
            Hook<CardZoneChangeEvent>();
            Hook<CardEnterActivationEvent>();
            Hook<CardLeaveActivationEvent>();
            Hook<CardActivationFailedEvent>();
            Hook<TokenCreatedEvent>();
            Hook<CounterChangedEvent>(); // 无效指示物增减 = 光环压制状态变化

            InvalidateCache();
        }

        /// <summary>组合根拆线（对齐 AdjacentResolver 归零惯例）：委托清空 + 退订 + 缓存清空。</summary>
        public static void Detach()
        {
            TryGetCellOf = null;
            CardAt = null;
            OwnerIndexOf = null;
            foreach (var sub in _subs) sub.Dispose();
            _subs.Clear();
            _cache.Clear();
        }

        // ======================================== 查询 API（受益者单视角） ========================================

        /// <summary>攻击力光环加成（含减益，可为负）</summary>
        public static int GetPowerBonus(Card card) => BonusOf(card)?.Power ?? 0;

        /// <summary>生命光环加成（上限与有效生命同加；可为负）</summary>
        public static int GetLifeBonus(Card card) => BonusOf(card)?.Life ?? 0;

        /// <summary>最大生命光环加成（与 GetLifeBonus 同值——生命光环上限当前同加）</summary>
        public static int GetMaxLifeBonus(Card card) => BonusOf(card)?.Life ?? 0;

        /// <summary>光环关键词（Boolean 语义：光环期间视为持有，不参与 GetKeywordCount 融合叠加计数）</summary>
        public static bool HasAuraKeyword(Card card, string keyword)
        {
            var bonus = BonusOf(card);
            return bonus != null && bonus.Keywords.Contains(keyword);
        }

        /// <summary>
        /// 手动失效（事件已自动覆盖常规路径；直改区域列表/手动 Resync 的测试场景调用）。
        /// 生命加成回落的受益者：裁剪溢出治疗（raw 不保留超出有效上限的部分）并泵一次 SBA
        /// （有效生命可能被压到 0——ZeroToughnessChecker 经层引擎基值含光环，会收尸）。
        /// </summary>
        public static void InvalidateCache()
        {
            var before = new List<(Card card, int lifeBonus)>(_cache.Count);
            foreach (var kv in _cache)
                before.Add((kv.Key, kv.Value.Life));

            _version++;
            _cache.Clear();

            if (before.Count == 0) return;
            bool anyLifeDrop = false;
            foreach (var (card, oldLifeBonus) in before)
            {
                if (card == null || !card.IsAlive) continue;
                int now = GetLifeBonus(card); // 重算（新鲜值）
                if (now >= oldLifeBonus) continue;
                anyLifeDrop = true;
                // 断链回落：溢出治疗部分不保留（当前 ≤ 有效上限）
                int effectiveMax = card._maxLife + now;
                if (card._life > effectiveMax) card._life = Math.Max(0, effectiveMax);
            }
            if (anyLifeDrop)
                GameCore.Instance?.SBAEngine?.CheckAndExecute();
        }

        // ======================================== 内部：live-query 计算 ========================================

        private static CachedBonus BonusOf(Card beneficiary)
        {
            if (beneficiary == null || !Enabled) return null;
            if (_cache.TryGetValue(beneficiary, out var cached) && cached.Version == _version)
                return cached;

            var bonus = new CachedBonus { Version = _version };
            var core = GameCore.Instance;
            var zoneManager = core?.ZoneManager;
            var bCell = TryGetCellOf(beneficiary);
            if (zoneManager != null && bCell.HasValue)
            {
                foreach (var player in new[] { core.Player1, core.Player2 })
                {
                    if (player == null) continue;
                    var battlefield = zoneManager.GetCards(player, Zone.Battlefield);
                    for (int i = 0; i < battlefield.Count; i++)
                        AccumulateFrom(battlefield[i], beneficiary, bCell.Value, bonus);
                }
            }

            _cache[beneficiary] = bonus; // 负缓存也入表（无光环卡读一次后 O(1)）
            return bonus;
        }

        private static void AccumulateFrom(Card source, Card beneficiary, (int x, int z) bCell, CachedBonus bonus)
        {
            if (source == null || !source.IsAlive || ReferenceEquals(source, beneficiary)) return;

            var data = (source as CardWrapper)?.GetData();
            var arrows = data?.ArrowDirections ?? HexDirection.None;
            if (arrows == HexDirection.None || data == null) return;
            if (source.GetCounterCount(CounterRules.NullifyCounter) > 0) return; // 无效=唯一能压光环的口

            var sCell = TryGetCellOf(source);
            if (!sCell.HasValue) return; // 来源未落格（不在场）自然无贡献
            int owner = OwnerIndexOf(source);
            if (owner < 0) return;

            foreach (var bit in ArrowBits)
            {
                if ((arrows & bit) == 0) continue;
                var abs = BoardMath.MapArrow(bit);
                if (owner == 1) abs = BoardMath.Opposite(abs); // 对手视角镜像（双方棋盘 180° 对称）
                var (nx, nz) = BoardMath.Neighbor(sCell.Value.x, sCell.Value.z, abs);
                if (!BoardMath.InBounds(nx, nz)) continue;
                if (!ReferenceEquals(CardAt(nx, nz), beneficiary)) continue;
                Accumulate(bonus, data.LinkAuras);
            }
        }

        private static void Accumulate(CachedBonus bonus, List<LinkAuraData> auras)
        {
            if (auras == null) return;
            foreach (var aura in auras)
            {
                if (aura == null) continue;
                if (!string.IsNullOrEmpty(aura.stat))
                {
                    if (aura.stat.Equals("Power", StringComparison.OrdinalIgnoreCase)) bonus.Power += aura.value;
                    else if (aura.stat.Equals("Life", StringComparison.OrdinalIgnoreCase)) bonus.Life += aura.value;
                }
                else if (!string.IsNullOrEmpty(aura.keyword) && !bonus.Keywords.Contains(aura.keyword))
                {
                    bonus.Keywords.Add(aura.keyword);
                }
            }
        }

        private static void Hook<T>() where T : class, IGameEvent
        {
            Action<T> handler = _ => InvalidateCache();
            EventManager.Instance.Subscribe(handler);
            _subs.Add(new Subscription<T>(handler));
        }

        /// <summary>延迟 Unsubscribe 的小包装（对齐 BoardState.Subscription 惯例）</summary>
        private sealed class Subscription<T> : IDisposable where T : class, IGameEvent
        {
            private readonly Action<T> _handler;
            public Subscription(Action<T> handler) => _handler = handler;
            public void Dispose() => EventManager.Instance.Unsubscribe(_handler);
        }
    }
}
