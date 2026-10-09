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
    ///
    /// 角色通道（2026-10-07 角色参战定案；2026-10-08 作用面含角色档）：**仅关键词**条目可投递角色
    /// ——箭头落格=本方角色格（BoardLayout.CharacterCell）时投递该角色；条目级作用面（scope 己/双/对方）
    /// 在条目声明 role 时同样投递对应侧角色。属性（攻/血）条目与未声明 role 的作用面条目钉死生物专用。
    /// 角色攻击力不走光环（HeroAttackCounter 弹药原子，见 CounterRules——角色攻击力增加原子表内
    /// 无连接位，不可作光环）。
    /// </summary>
    public static class LinkAuraSystem
    {
        // —— 组合根注入的占用查询（核心不绑棋盘：静态委托扩展点）——
        public static Func<Card, (int x, int z)?> TryGetCellOf { get; private set; }
        public static Func<int, int, Card> CardAt { get; private set; }
        public static Func<Card, int> OwnerIndexOf { get; private set; }
        /// <summary>玩家序号查询（2026-10-07 角色光环通道）：角色格=BoardLayout.CharacterCell(idx)，
        /// 关键词光环箭头指向角色格时投递给该角色。</summary>
        public static Func<Player, int> PlayerIndexOf { get; private set; }

        /// <summary>是否已注入棋盘（未注入 = 无光环，所有查询 O(1) 早退）</summary>
        public static bool Enabled => TryGetCellOf != null && CardAt != null && OwnerIndexOf != null && PlayerIndexOf != null;

        private static int _version; // 失效版本号（事件驱动 +1；缓存按版本号判脏）
        private static readonly Dictionary<Entity, CachedBonus> _cache = new Dictionary<Entity, CachedBonus>();
        private static readonly List<IDisposable> _subs = new List<IDisposable>();

        private sealed class CachedBonus
        {
            public int Version;
            public int Power;
            public int Life;
            // 关键词值求和表 KeywordSums 已删（2026-10-08 坚韧指示物化）：唯一消费者是坚韧光环减伤，
            // 坚韧退出关键词/光环族后无值语义关键词条目——只剩 Boolean（HasAuraKeyword）与计数
            //（GetAuraKeywordCount，按箭头叠加）两种读数
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
            PlayerIndexOf = board.PlayerIndex;

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
            PlayerIndexOf = null;
            foreach (var sub in _subs) sub.Dispose();
            _subs.Clear();
            _cache.Clear();
        }

        // ======================================== 查询 API（受益者单视角） ========================================
        // 属性三口（GetPowerBonus/GetLifeBonus/GetMaxLifeBonus）钉死 Card 签名——属性光环只对生物生效
        //（2026-10-07 定案：角色攻击力走 HeroAttackCounter 弹药原子，不走光环；生命光环同理不到角色）。
        // 关键词三口 Entity 签名——关键词条目可投递角色（箭头指向角色格，或条目级作用面+含角色声明）。

        /// <summary>攻击力光环加成（含减益，可为负；仅生物）</summary>
        public static int GetPowerBonus(Card card) => BonusOf(card)?.Power ?? 0;

        /// <summary>生命光环加成（上限与有效生命同加；可为负；仅生物）</summary>
        public static int GetLifeBonus(Card card) => BonusOf(card)?.Life ?? 0;

        /// <summary>最大生命光环加成（与 GetLifeBonus 同值——生命光环上限当前同加；仅生物）</summary>
        public static int GetMaxLifeBonus(Card card) => BonusOf(card)?.Life ?? 0;

        /// <summary>光环关键词（Boolean 语义：光环期间视为持有，不参与 GetKeywordCount 重复叠加计数）。
        /// Entity 签名（2026-10-07）：角色经箭头指向角色格的关键词光环同样命中。</summary>
        public static bool HasAuraKeyword(Entity entity, string keyword)
        {
            var bonus = BonusOf(entity);
            return bonus != null && bonus.Keywords.Contains(keyword);
        }

        /// <summary>光环关键词覆盖数（2026-09-13 按箭头叠加定案）：N 条箭头（×每源声明条数）
        /// 覆盖同一单位 → N。纯计数读数（值求和口 GetAuraKeywordSum 已随 2026-10-08 坚韧指示物化删除
        /// ——值语义关键词已不存在）；Boolean 走 HasAuraKeyword。</summary>
        public static int GetAuraKeywordCount(Entity entity, string keyword)
        {
            var bonus = BonusOf(entity);
            if (bonus == null) return 0;
            int n = 0;
            foreach (var kw in bonus.Keywords)
                if (kw == keyword) n++;
            return n;
        }

        // 值求和口 GetAuraKeywordSum 已删（2026-10-08 坚韧指示物化）：唯一消费者是
        // KeywordRules.ApplyPreventionLayers 的坚韧光环份额——坚韧改挂 ToughnessCounter 指示物，
        // 不再经关键词光环投递（表行 2b1e3700 MountKinds=指示物，CanMountAsAura 位 1 硬拒）。

        // 守护光环源查询（GetGuardianAuraSources）已随 2026-10-08 配对制改版退役——
        // 守护退出连接光环族（表行 MountKinds 位 8 拉黑），改写走 GuardianRules 配对表（KeywordRules.ApplyDamage）。

        /// <summary>方向档命中·生物（2026-10-07）：beneficiary 一侧是否在 source 光环方向内
        ///（1=己方：源控制者一侧；2=双方；3=对方：源控制者的对手一侧）。</summary>
        private static bool ScopeHit(Card source, Card beneficiary, int scope)
        {
            if (scope < 1 || scope > 3) return false;
            var sc = source.GetController();
            var bc = beneficiary.GetController();
            if (sc == null || bc == null) return false;
            if (scope == 2) return true;
            return scope == 1 ? ReferenceEquals(sc, bc) : !ReferenceEquals(sc, bc);
        }

        /// <summary>方向档命中·角色（2026-10-08 含角色定案）：作用面档条目声明 role 时按侧别投递
        ///（1=源控制者本人的角色/2=双方角色/3=对手角色）。</summary>
        private static bool ScopeHitRole(Card source, Player role, int scope)
        {
            if (scope < 1 || scope > 3) return false;
            var sc = source.GetController();
            if (sc == null || role == null) return false;
            if (scope == 2) return true;
            return scope == 1 ? ReferenceEquals(sc, role) : ReferenceEquals(sc.Opponent, role);
        }

        /// <summary>source 卡面箭头几何是否命中 beneficiary（keywordsOnly=false：占据者同一=认格不认主；
        /// true：坐标即身份——角色格永无卡占据者，落格即命中本方角色格）。</summary>
        private static bool ArrowsGeoHit(Card source, (int x, int z) bCell, Entity beneficiary, bool keywordsOnly)
        {
            var data0 = (source as CardWrapper)?.GetData();
            var arrows = data0?.ArrowDirections ?? HexDirection.None;
            if (arrows == HexDirection.None) return false;
            var sCell = TryGetCellOf(source);
            if (!sCell.HasValue) return false;
            int owner = OwnerIndexOf(source);
            if (owner < 0) return false;

            foreach (var bit in ArrowBits)
            {
                if ((arrows & bit) == 0) continue;
                var abs = BoardMath.MapArrow(bit);
                if (owner == 1) abs = BoardMath.Opposite(abs);
                var (nx, nz) = BoardMath.Neighbor(sCell.Value.x, sCell.Value.z, abs);
                if (!BoardMath.InBounds(nx, nz)) continue;
                if (keywordsOnly
                    ? (nx == bCell.x && nz == bCell.z)
                    : ReferenceEquals(CardAt(nx, nz), beneficiary))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 手动失效（事件已自动覆盖常规路径；直改区域列表/手动 Resync 的测试场景调用）。
        /// 生命加成回落的受益者：裁剪溢出治疗（raw 不保留超出有效上限的部分）并泵一次 SBA
        /// （有效生命可能被压到 0——ZeroToughnessChecker 经层引擎基值含光环，会收尸）。
        /// </summary>
        public static void InvalidateCache()
        {
            // 生命回落兜底只对生物受益者有意义（角色路径 Life 恒 0——属性光环不投递角色）
            var before = new List<(Card card, int lifeBonus)>(_cache.Count);
            foreach (var kv in _cache)
                if (kv.Key is Card c) before.Add((c, kv.Value.Life));

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

        private static CachedBonus BonusOf(Entity beneficiary)
        {
            if (beneficiary == null || !Enabled) return null;
            if (_cache.TryGetValue(beneficiary, out var cached) && cached.Version == _version)
                return cached;

            var bonus = new CachedBonus { Version = _version };
            if (beneficiary is Card card)
            {
                var bCell = TryGetCellOf(card);
                if (bCell.HasValue)
                    AccumulateSources(beneficiary, bCell.Value, bonus, keywordsOnly: false);
            }
            else if (beneficiary is Player role)
            {
                // 角色通道（2026-10-07 定案）：受益格=本方角色格（BoardLayout.CharacterCell）；
                // 只收关键词条目（keywordsOnly）——属性光环钉死生物专用、作用面档永不投递角色。
                int idx = PlayerIndexOf(role);
                if (idx >= 0)
                    AccumulateSources(role, BoardLayout.CharacterCell(idx), bonus, keywordsOnly: true);
            }

            _cache[beneficiary] = bonus; // 负缓存也入表（无光环卡读一次后 O(1)）
            return bonus;
        }

        private static void AccumulateSources(Entity beneficiary, (int x, int z) bCell, CachedBonus bonus, bool keywordsOnly)
        {
            var core = GameCore.Instance;
            var zoneManager = core?.ZoneManager;
            if (zoneManager == null) return;
            foreach (var player in new[] { core.Player1, core.Player2 })
            {
                if (player == null) continue;
                var battlefield = zoneManager.GetCards(player, Zone.Battlefield);
                for (int i = 0; i < battlefield.Count; i++)
                    AccumulateFrom(battlefield[i], beneficiary, bCell, bonus, keywordsOnly);
            }
        }

        private static void AccumulateFrom(Card source, Entity beneficiary, (int x, int z) bCell, CachedBonus bonus, bool keywordsOnly)
        {
            if (source == null || !source.IsAlive || ReferenceEquals(source, beneficiary)) return;

            var data = (source as CardWrapper)?.GetData();
            if (data?.LinkAuras == null || data.LinkAuras.Count == 0) return;
            if (source.GetCounterCount(CounterRules.NullifyCounter) > 0) return; // 无效=唯一能压光环的口

            // 条目级作用面（2026-10-08 actuating-range 定案）：scope==0 随卡面箭头几何（生物=占据者同一/
            // 角色=落格即本方角色格）；作用面档（1=己方/2=双方/3=对方）按侧别命中。旧卡级方向档已由
            // AggregateEffectAuras 迁移盖戳到条目，此处不再读 data.AuraScope。
            // 角色通道（2026-10-09 裁定）：关键词光环**一律可作用角色**——不能用光环表达的行
            // （守护/再生/禁魔石）已由表「不可作为连接光环」位拉黑，通道不再设防；逐条目 role 声明
            // 与 NoRole 硬闸（RoleChannelBlocked）整体退役；属性条目恒仅生物（2026-10-07 定案不变）。
            var arrows = data.ArrowDirections;
            bool geoComputed = false, geoHit = false; // 惰性：存在箭头档条目才算几何
            foreach (var aura in data.LinkAuras)
            {
                if (aura == null) continue;
                if (aura.scope > 0)
                {
                    if (keywordsOnly)
                    {
                        if (beneficiary is Player pr && ScopeHitRole(source, pr, aura.scope))
                            Accumulate(bonus, aura, keywordsOnly);
                    }
                    else if (beneficiary is Card bCard && ScopeHit(source, bCard, aura.scope))
                        Accumulate(bonus, aura, keywordsOnly);
                    continue;
                }
                if (arrows == HexDirection.None) continue;
                if (!geoComputed) { geoHit = ArrowsGeoHit(source, bCell, beneficiary, keywordsOnly); geoComputed = true; }
                if (geoHit)
                    Accumulate(bonus, aura, keywordsOnly);
            }
        }

        private static void Accumulate(CachedBonus bonus, LinkAuraData aura, bool keywordsOnly)
        {
            if (aura == null) return;
            if (!string.IsNullOrEmpty(aura.stat))
            {
                if (keywordsOnly) return; // 属性光环只对生物生效（2026-10-07 定案：角色攻击力走弹药原子）
                if (aura.stat.Equals("Power", StringComparison.OrdinalIgnoreCase)) bonus.Power += aura.value;
                else if (aura.stat.Equals("Life", StringComparison.OrdinalIgnoreCase)) bonus.Life += aura.value;
                else if (aura.stat.Equals("Both", StringComparison.OrdinalIgnoreCase))
                {
                    // 属性光环（2026-10-07 深夜六类）：攻生同值修正——「属性增加/减少」源行天然 ±1/±1
                    bonus.Power += aura.value;
                    bonus.Life += aura.value;
                }
            }
            else if (!string.IsNullOrEmpty(aura.keyword))
            {
                // 2026-09-13 按箭头叠加定案：关键词不去重——每条命中箭头×每条声明各计一次
                //（Boolean 查询 HasAuraKeyword 用 Contains；计数查询 GetAuraKeywordCount；
                // 值求和口已随 2026-10-08 坚韧指示物化删除——aura.value 对关键词条目不再有读数方）
                bonus.Keywords.Add(aura.keyword);
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
