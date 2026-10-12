using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 元素池中的卡牌（地牌，锁色产色体）——2026-10-11 资源系统重做定案：
    /// 指示物整套退役。入池时从卡牌费用颜色（红/蓝/绿/灰）中自选锁定一个产色，后续生成固定；
    /// 地牌不消耗、不耗竭、不入墓（移除口=摧毁/抹除效果）。
    /// 付费时系统按账单自动横置（强制系统动作，无速度交互，自己/对手回合都一样），
    /// 每地牌每回合周期恰好横置一次，产 1 个锁定色元素**直接支付**（不入库）。
    /// </summary>
    public class PooledCard
    {
        public Card SourceCard { get; }

        /// <summary>入池时锁定的产色（红/蓝/绿/灰——黑白不由地牌产出）</summary>
        public ManaType LockedColor { get; }

        /// <summary>本回合周期已横置（已为某次支付贡献过 1 个元素）。己方回合开始时恢复（解除横置）。</summary>
        public bool IsTapped { get; set; }

        public PooledCard(Card card, ManaType lockedColor)
        {
            SourceCard = card;
            LockedColor = lockedColor;
        }
    }

    /// <summary>
    /// 玩家元素池数据
    /// </summary>
    public class PlayerElementPool
    {
        /// <summary>场上地牌（锁色产色体）。张数受各自地牌槽上限约束（见 GetLandCap）——
        /// 2026-10-11 定案：10-10 的全场共享计数器废止，回归各自上限。</summary>
        public List<PooledCard> PooledCards { get; } = new List<PooledCard>();

        /// <summary>临时钱包（2026-10-11 定案：bank 退役）：效果授予的元素（代价补偿黑白/错边转化/
        /// 光合作用/带地牌特性生物产色）落这里，**任一回合结束清空**。付费时与地牌横置一起被规划消耗。</summary>
        public Dictionary<ManaType, int> TempElements { get; } = new Dictionary<ManaType, int>();

        // ===== 地牌槽曲线（Cap 管场上地牌张数）=====

        /// <summary>该玩家的资源曲线（由 GameCore.InitGame 注入固定地牌槽曲线；变速效果经 SetCurve 修改）</summary>
        public ResourceCurve Curve { get; set; }

        /// <summary>最近的全局回合数（纯台账/观测用——2026-10-11 起上限改读 PersonalTurnIndex）</summary>
        public int GlobalTurnIndex { get; set; }

        /// <summary>个人回合数（自己回合开始时 +1；地牌槽曲线 CapAt 的索引——2026-10-11 定案：
        /// 自己的回合开始地牌上限 +1，个人口径）</summary>
        public int PersonalTurnIndex { get; set; }

        /// <summary>「成长」原子累积的地牌上限加值（2026-10-11 定案：灵气复苏×2翻倍改成长+1，
        /// 可重复使用，各自池生效不共享；GetLandCap 输出封顶 MaxStackedLandCap=18）。</summary>
        public int LandCapBonus { get; set; }

        /// <summary>本回合系统横置次数（付费自动横置；每个全局回合重置——纯观测）</summary>
        public int TapsThisTurn { get; set; }

        /// <summary>本回合已获得黑元素数（黑白合计生成钳制台账：黑+白 ≤ 地牌上限）</summary>
        public int BlackGainedThisTurn { get; set; }

        /// <summary>本回合已获得白元素数（同上）</summary>
        public int WhiteGainedThisTurn { get; set; }

        public PlayerElementPool()
        {
            foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
            {
                TempElements[mana] = 0;
            }
        }
    }

    /// <summary>
    /// 元素池系统——2026-10-11 资源系统重做定案（现取现用）：
    /// 1. 地牌 = 锁色产色体：入池时自选卡费颜色之一锁定；不消耗不耗竭；移除口=摧毁/抹除效果。
    /// 2. 地牌上限 = 自己回合开始 +1（个人曲线 CapAt(PersonalTurnIndex)，标准曲线 min(个人回合,9)）；
    ///    「成长」原子 +1/次封顶 18。各自上限管各自地牌张数（共享计数器废止）。
    /// 3. 付费 = 现取现用：按账单规划「临时钱包余额 + 未横置地牌横置」组合，钱包优先消耗（回合末即逝），
    ///    地牌横置=系统自动的强制动作（无速度交互），自己/对手回合都一样（跨回合支付不变）。
    /// 4. 支付链（2026-10-11 收窄）：红/蓝/绿仅本色；灰=[灰,黑,白,红,蓝,绿]（黑白只垫灰、三色垫灰沿用）；
    ///    黑白本色费只有黑白元素能付（单向不变）。浓度上限维持：每货币单次贡献 ≤ 地牌上限、纯色需求超帽拒付。
    /// 5. 临时钱包：效果授予元素的唯一归宿，任一回合结束清空；黑白合计每回合生成 ≤ 地牌上限（单钳，
    ///    旧三重钳退役）。
    /// 6. 黑白不由地牌产出（锁定色不含黑白）；获取通道=代价补偿/错边转化等卡结算路径。
    /// 7. 发动无效与地牌零交互（净零靠付费在响应窗之后，本就无退款路径）；地牌的交互面只有摧毁/抹除。
    /// </summary>
    public class ElementPoolSystem
    {
        /// <summary>地牌槽上限的最大值（曲线末端值；卡费用上限同为 9）</summary>
        public const int MAX_POOL_SIZE = 9;

        /// <summary>地牌上限硬顶（成长原子叠加的封顶值）：9 格 × 每格叠两层 = 18。
        /// 挑战模式 P2 加成曲线（9+bonus）再叠加同样受此钳制。</summary>
        public const int MaxStackedLandCap = 18;

        private Dictionary<Player, PlayerElementPool> _playerPools =
            new Dictionary<Player, PlayerElementPool>();

        // ===== 曲线事件路由 =====
        // 新事件（CurveShiftEvent）经 GameCore 统一路由发布，保证 Trigger/Layer 引擎可见。
        private Action<IGameEvent> _eventRouter;

        // ======================================== 初始化 ========================================

        /// <summary>
        /// 注入事件路由（GameCore.Initialize 调用，指向 GameCore 的统一发布路径）。
        /// </summary>
        public void AttachEventRouter(Action<IGameEvent> router)
        {
            _eventRouter = router;
        }

        private void PublishRouted(IGameEvent e)
        {
            if (_eventRouter != null)
                _eventRouter(e);
            else
                EventManager.Instance.PublishDynamic(e);
        }

        public void InitializePlayer(Player player)
        {
            if (!_playerPools.ContainsKey(player))
            {
                _playerPools[player] = new PlayerElementPool();
            }
        }

        public PlayerElementPool GetPool(Player player)
        {
            if (!_playerPools.ContainsKey(player))
                InitializePlayer(player);
            return _playerPools[player];
        }

        // ======================================== 地牌资格与入池 ========================================

        /// <summary>
        /// 地牌资格（2026-10-03 定案沿用）：卡组正式 生物/法术/结界 三型可作地牌——
        /// 衍生物（IsToken，SummonToken 实例）、临时复制卡（回响——持临时指示物 TempCounter）不可
        /// （否则副本成免费地牌，2026-10-11 维持禁令）。
        /// UI/AI 预检与 AddCardToPool 权威校验共用此判据。
        /// </summary>
        public static bool CanServeAsLand(Card card)
            => card is CardWrapper
               && card.GetCounterCount(Attribute.CounterRules.TempCounter) <= 0 // 临时复制卡（临时指示物标记）
               && !card.IsToken
               && card is IHasSupertype ht
               && (ht.Supertype == Cardtype.Creature
                   || ht.Supertype == Cardtype.Spell
                   || ht.Supertype == Cardtype.Enchantment);

        /// <summary>
        /// 地牌可锁定的产色候选（2026-10-11 定案）：卡牌**声明费用**中的红/蓝/绿/灰颜色集合
        /// （黑白不由地牌产出；抉择卡按声明费用取色，不再按所选模式）。
        /// 按费用份额降序排列（并列取枚举序靠前者）——缺省锁色（colorChoice=null）取首位=主色。
        /// 空候选（0 费卡/纯黑白费卡）不可入池。
        /// </summary>
        public static List<ManaType> GetLandColorChoices(Card card)
            => card is IHasCost hasCost ? GetLandColorChoices(hasCost) : new List<ManaType>();

        /// <summary>同 GetLandColorChoices（IHasCost 重载——CardData 同接口，UI 网络侧无引擎对象时直用卡数据）。</summary>
        public static List<ManaType> GetLandColorChoices(IHasCost hasCost)
        {
            var pairs = new List<(ManaType color, int amount)>();
            if (hasCost?.Cost != null)
            {
                foreach (var type in hasCost.Cost.NonzeroColors())
                {
                    int amount = (int)hasCost.Cost[type];
                    if (amount > 0 && type != ManaType.Black && type != ManaType.White)
                        pairs.Add((type, amount));
                }
            }
            return pairs
                .OrderByDescending(p => p.amount)
                .ThenBy(p => (int)p.color)
                .Select(p => p.color)
                .ToList();
        }

        /// <summary>入池预检（UI/AI 共用单点）：资格由 CanServeAsLand 另查；这里只看各自上限余位。</summary>
        public bool CanAddLand(Player owner)
            => GetPool(owner).PooledCards.Count < GetLandCap(owner);

        /// <summary>
        /// 将手牌作为地牌放入元素池（主阶段调用，可用/未横置状态入场，不限张数/次数）。
        /// 张数受**各自**地牌槽上限约束（2026-10-11：个人曲线 min(个人回合,9)+成长加值，封顶 18）。
        /// colorChoice：从 GetLandColorChoices 中自选锁定产色；null=缺省主色（份额最多色）。
        /// 地牌不消耗不耗竭；离池（弹回手等）后可再入池重新选色。
        /// </summary>
        public bool AddCardToPool(Card card, Player owner, ManaType? colorChoice = null)
        {
            if (card == null || owner == null) return false;

            // 地牌资格：卡组正式三型（衍生物/临时复制卡不可）
            if (!CanServeAsLand(card))
                return false;

            var pool = GetPool(owner);

            // 地牌槽上限（各自口径）
            if (pool.PooledCards.Count >= GetLandCap(owner))
                return false;

            // 锁色候选：卡费颜色（去黑白）；空候选（0费/纯黑白）拒绝入池
            var choices = GetLandColorChoices(card);
            if (choices.Count == 0)
                return false;

            // 显式锁色必须在候选内（非法选择拒绝——UI 只给有效项，AI/夹具传错即拒）
            if (colorChoice.HasValue && !choices.Contains(colorChoice.Value))
                return false;

            var color = colorChoice ?? choices[0]; // 缺省=主色（份额最多色）

            pool.PooledCards.Add(new PooledCard(card, color));

            // 事件发布延后（2026-09-26 时序定案沿用）：入池事件不在此发——调用方完成
            // Zone.ElementPool 区域移动后调 PublishPoolAdd。
            return true;
        }

        /// <summary>发布地牌入池事件：调用方在 Zone.ElementPool 区域移动完成后调用
        /// （AddCardToPool 本体不发——见其尾注；事件携带锁定产色）。</summary>
        public void PublishPoolAdd(Card card, Player owner)
        {
            var pooled = GetPool(owner).PooledCards.FirstOrDefault(pc => pc.SourceCard == card);
            PublishEvent(new ElementPoolAddEvent
            {
                Player = owner,
                AddedCard = card,
                LockedColor = pooled?.LockedColor ?? ManaType.Gray,
            });
        }

        /// <summary>
        /// 将池内地牌移出元素池（弹回手等效果使用）。地牌无余量状态——再入池重新选色。
        /// 只移除池内记录，不动区域 —— 区域移动由调用方完成。
        /// </summary>
        public bool RemoveCardFromPool(Card card, Player owner)
        {
            if (card == null || owner == null) return false;

            var pool = GetPool(owner);
            var pooledCard = pool.PooledCards.FirstOrDefault(pc => pc.SourceCard == card);
            if (pooledCard == null)
                return false;

            pool.PooledCards.Remove(pooledCard);
            return true;
        }

        // ======================================== 地牌槽曲线管理 ========================================

        /// <summary>
        /// 设置玩家的资源曲线（GameCore.InitGame 注入固定地牌槽曲线；变速效果经此修改）。
        /// 发布 CurveShiftEvent（经路由，Trigger/Layer 可见）。
        /// </summary>
        public void SetCurve(Player player, ResourceCurve curve, string reason = null)
        {
            if (player == null) return;
            var pool = GetPool(player);
            pool.Curve = curve;

            PublishRouted(new CurveShiftEvent
            {
                Player = player,
                NewCurve = curve,
                Reason = reason
            });
        }

        /// <summary>
        /// 「成长」原子（2026-10-11 定案：灵气复苏 ×2 翻倍改成长 +1）：地牌上限 +1，
        /// 各自池生效不共享、可重复使用；输出封顶 MaxStackedLandCap（18，每地牌格叠两层）。
        /// 已到硬顶返回 false。作用于 GetLandCap 单点：入池闸/支付浓度/费用门槛/黑白钳/AI/快照同随。
        /// </summary>
        public bool AddLandCapBonus(Player player)
        {
            if (player == null) return false;
            var pool = GetPool(player);
            if (GetLandCap(player) >= MaxStackedLandCap) return false; // 封顶钳制
            pool.LandCapBonus++;
            PublishRouted(new CurveShiftEvent
            {
                Player = player,
                NewCurve = pool.Curve, // 曲线对象未换——事件仅作 Trigger/Layer 可见的通知载体
                Reason = "成长:地牌上限+1（封顶18）",
            });
            return true;
        }

        /// <summary>
        /// 当前的地牌槽上限（2026-10-11 定案：**个人曲线**——自己回合开始 +1，读 PersonalTurnIndex；
        /// 10-10 全场共享计数器废止，各自上限管各自地牌张数）。
        /// 标准曲线 = min(个人回合数, 9)；成长原子加值 LandCapBonus 封顶 18。
        /// 无曲线时按同一公式兜底。挑战模式 P2 加成曲线照常各自注入。
        /// </summary>
        public int GetLandCap(Player player)
        {
            var pool = GetPool(player);
            int cap;
            if (pool.Curve != null)
                cap = pool.Curve.CapAt(pool.PersonalTurnIndex);
            else
                cap = Math.Max(1, Math.Min(pool.PersonalTurnIndex, MAX_POOL_SIZE));
            return Math.Min(cap + pool.LandCapBonus, MaxStackedLandCap);
        }

        // ======================================== 临时钱包（效果授予元素） ========================================

        /// <summary>效果发放（代价补偿/错边/光合作用/带地牌特性生物产色等 AddMana 路径）——
        /// 落**临时钱包**（任一回合结束清空）。黑白合计生成钳制（2026-10-11 定案：旧三重钳退役，
        /// 只留单钳）：本回合黑+白合计获得 ≤ 地牌上限，钳掉部分不入账不发事件；三色/灰不钳。
        /// 返回实发量（钳后）。</summary>
        public int AddMana(Player player, ManaType type, Card fromCard, int amount = 1)
        {
            if (player == null || amount <= 0) return 0;
            var pool = GetPool(player);

            if (type == ManaType.Black || type == ManaType.White)
            {
                amount = PreviewGrantAmount(player, type, amount);
                if (amount <= 0) return 0;
                if (type == ManaType.Black) pool.BlackGainedThisTurn += amount;
                else pool.WhiteGainedThisTurn += amount;
            }

            pool.TempElements[type] += amount;
            PublishEvent(new ElementPoolGainEvent
            {
                Player = player,
                GainedType = type,
                FromCard = fromCard,
                Amount = amount
            });
            return amount;
        }

        /// <summary>有效获得量预览（代价垫色门槛用）：镜像 AddMana 的黑白合计钳制纯函数
        /// （黑+白 本回合合计 ≤ 地牌上限），不动任何状态；三色/灰原样返回。
        /// 声明期 CanAffordCast 以此虚拟并入钱包——不虚估（被钳部分垫不了卡费）。</summary>
        public int PreviewGrantAmount(Player player, ManaType type, int amount)
        {
            if (player == null || amount <= 0) return 0;
            if (type != ManaType.Black && type != ManaType.White) return amount;
            var pool = GetPool(player);
            int cap = GetLandCap(player);
            int combined = pool.BlackGainedThisTurn + pool.WhiteGainedThisTurn; // 黑白合计（2026-10-11 单钳）
            return Math.Max(0, Math.Min(amount, cap - combined));
        }

        // ======================================== 支付（现取现用统一规划器） ========================================

        /// <summary>支付规划结果：钱包扣减 + 横置地牌清单 + 实付组合（事件用）。</summary>
        private sealed class PaymentPlan
        {
            public readonly Dictionary<ManaType, int> WalletUse = new Dictionary<ManaType, int>();
            public readonly List<PooledCard> Taps = new List<PooledCard>();
            public readonly Dictionary<ManaType, int> TotalPaid = new Dictionary<ManaType, int>();
        }

        /// <summary>
        /// 支付预检（声明期门禁用，纯函数不动状态）：临时钱包（+seed 虚拟并入）加未横置地牌
        /// 能否覆盖账单。seed（2026-10-11 代价垫色沿用）：代价补偿的声明期门槛用，不实际入池。
        /// </summary>
        public bool CanPayCostWithAutoTap(ElementCost cost, Player player)
            => CanPayCostWithAutoTap(ElementPaymentValidator.NormalizeBill(cost), player);

        /// <summary>同 CanPayCostWithAutoTap（已归一账单入口——GameActions.CanAfford 合并 pending 声明承诺后调用）。</summary>
        public bool CanPayCostWithAutoTap(Dictionary<ManaType, int> bill, Player player,
            Dictionary<ManaType, int> seed = null)
        {
            var pool = GetPool(player);
            var wallet = SnapshotWallet(pool);
            if (seed != null && seed.Count > 0)
                foreach (var kv in seed)
                    wallet[kv.Key] = wallet.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
            return PlanBill(bill, wallet, UntappedLands(pool), GetLandCap(player)) != null;
        }

        /// <summary>
        /// 支付费用（付费步统一入口——出牌施放结算 / 英雄技能 / 效果费）：
        /// 按账单规划「钱包消耗 + 系统自动横置」并原子执行（现取现用——元素不落库直接付）。
        /// 地牌横置=系统强制动作（无速度交互，自己/对手回合都一样；每地牌每回合一次）。
        /// 返回 false = 规划失败（钱包+可横置地牌补不足），此情况下**不动任何状态**（先纯规划后执行）。
        /// </summary>
        public bool TryPayCostWithAutoTap(ElementCost cost, Player player, string sourceNote = null)
            => TryPayBillWithAutoTap(ElementPaymentValidator.NormalizeBill(cost), player, sourceNote);

        /// <summary>同 TryPayCostWithAutoTap（已归一账单入口——ElementCostPayment 聚合需求后调用）。</summary>
        public bool TryPayBillWithAutoTap(Dictionary<ManaType, int> bill, Player player, string sourceNote = null)
        {
            var pool = GetPool(player);
            var plan = PlanBill(bill, SnapshotWallet(pool), UntappedLands(pool), GetLandCap(player));
            if (plan == null) return false;

            foreach (var kv in plan.WalletUse)
                pool.TempElements[kv.Key] -= kv.Value;

            foreach (var land in plan.Taps)
            {
                land.IsTapped = true;
                pool.TapsThisTurn++;
            }

            // PaidCost 携带实付组合（钱包+横置合计口径）——三相光环/台账按真实货币计数
            PublishEvent(new ElementPoolPayEvent
            {
                Player = player,
                PaidCost = plan.TotalPaid.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value),
                SourceNote = sourceNote,
            });

            return true;
        }

        /// <summary>未横置的地牌（本回合周期尚可为支付贡献 1 个锁定色元素）。</summary>
        private static List<PooledCard> UntappedLands(PlayerElementPool pool)
            => pool.PooledCards.Where(pc => pc != null && !pc.IsTapped).ToList();

        private static Dictionary<ManaType, int> SnapshotWallet(PlayerElementPool pool)
            => new Dictionary<ManaType, int>(pool.TempElements);

        /// <summary>
        /// 账单支付规划（纯函数、非破坏）：求「钱包扣减 + 地牌横置」组合使账单可付；null = 无法补足。
        /// 口径与旧 GetBillPaymentPlan+PlanAutoTaps 两段合一（2026-10-11 现取现用定案）：
        /// ① 需求侧浓度上限——纯色（红蓝绿黑白）需求 &gt; cap 直接不可付；
        /// ② 黑白本色费先行预留（黑白只来自钱包——地牌锁定色不含黑白）；
        /// ③ 按 FourColorOrder×ChainFor 贪心扣减（支付链 2026-10-11 收窄：三色仅本色；灰=灰→黑→白→三色）；
        /// ④ 每货币（含灰）单次贡献 ≤ cap；同货币先扣钱包（临时元素回合末即逝，优先消耗）再横置地牌。
        /// </summary>
        private static PaymentPlan PlanBill(
            Dictionary<ManaType, int> bill, Dictionary<ManaType, int> wallet,
            List<PooledCard> untapped, int cap)
        {
            if (bill == null || bill.Count == 0) return new PaymentPlan();

            // ① 需求侧浓度上限：纯色需求量超帽，混付也救不了（灰需求无需求侧帽，沿用旧口径）
            foreach (var kv in bill)
                if (kv.Key != ManaType.Gray && kv.Value > cap)
                    return null;

            var walletSim = new Dictionary<ManaType, int>();
            var landsOf = new Dictionary<ManaType, Queue<PooledCard>>();
            var used = new Dictionary<ManaType, int>();
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
            {
                walletSim[c] = wallet.TryGetValue(c, out var v) ? v : 0;
                used[c] = 0;
            }
            foreach (var land in untapped)
            {
                if (!landsOf.TryGetValue(land.LockedColor, out var queue))
                    landsOf[land.LockedColor] = queue = new Queue<PooledCard>();
                queue.Enqueue(land);
            }

            var plan = new PaymentPlan();

            void Record(ManaType currency, int take, PooledCard land)
            {
                used[currency] += take;
                plan.TotalPaid[currency] = plan.TotalPaid.TryGetValue(currency, out var prev) ? prev + take : take;
                if (land == null)
                {
                    walletSim[currency] -= take;
                    plan.WalletUse[currency] = plan.WalletUse.TryGetValue(currency, out var w) ? w + take : take;
                }
                else
                {
                    plan.Taps.Add(land);
                }
            }

            // ② 黑白本色费先行预留（防后续贪心吃掉黑白货币；黑白不垫任何色——单向）
            foreach (var bw in new[] { ManaType.Black, ManaType.White })
            {
                if (!bill.TryGetValue(bw, out var bwNeed) || bwNeed <= 0) continue;
                if (walletSim[bw] < bwNeed) return null;
                Record(bw, bwNeed, null);
            }

            // ③④ 四色需求按固定序×填链贪心：钱包优先、后横置地牌
            foreach (var need in ElementPaymentValidator.FourColorOrder)
            {
                if (!bill.TryGetValue(need, out var remaining) || remaining <= 0) continue;
                foreach (var currency in ElementPaymentValidator.ChainFor(need))
                {
                    if (remaining <= 0) break;
                    int room = cap - used[currency];
                    int have = walletSim[currency];
                    int take = Math.Min(Math.Min(have, remaining), room);
                    if (take > 0)
                    {
                        Record(currency, take, null);
                        remaining -= take;
                        room -= take;
                    }
                    if (remaining > 0 && landsOf.TryGetValue(currency, out var queue))
                    {
                        while (remaining > 0 && queue.Count > 0 && room > 0)
                        {
                            Record(currency, 1, queue.Dequeue());
                            remaining--;
                            room--;
                        }
                    }
                }
                if (remaining > 0) return null;
            }

            return plan;
        }

        // ======================================== 回合管理 ========================================

        /// <summary>
        /// 全局回合开始（GameCore.OnTurnStarted 调用，每回合一次，无论轮到谁）：
        /// 1. 回合玩家个人回合数 +1（地牌上限曲线推进——2026-10-11 个人口径）
        /// 2. 全局回合台账同步、所有玩家本回合产出/黑白获得台账清零
        /// 3. 回合玩家（准备阶段）地牌全部解除横置
        /// </summary>
        public void OnTurnStart(Player turnPlayer, int globalTurnNumber)
        {
            if (turnPlayer == null) return;

            var turnPool = GetPool(turnPlayer);
            turnPool.PersonalTurnIndex++;

            foreach (var pool in _playerPools.Values)
            {
                if (globalTurnNumber > pool.GlobalTurnIndex)
                    pool.GlobalTurnIndex = globalTurnNumber;
                pool.TapsThisTurn = 0;
                pool.BlackGainedThisTurn = 0;
                pool.WhiteGainedThisTurn = 0;
            }

            foreach (var pc in turnPool.PooledCards)
                pc.IsTapped = false;
        }

        /// <summary>
        /// 回合结束（GameCore.OnTurnEnded 调用）：**临时钱包清空**（2026-10-11 定案：任一回合结束，
        /// 未消耗的效果授予元素即逝——现取现用）。地牌无耗竭清理（旧自动产色/耗尽送墓已随指示物退役）。
        /// </summary>
        public void OnTurnEnd(Player turnPlayer)
        {
            if (turnPlayer == null) return;
            foreach (var pool in _playerPools.Values)
            {
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                    pool.TempElements[mana] = 0;
            }
        }

        // ======================================== 查询 ========================================

        /// <summary>临时钱包中指定颜色的数量（快照/观测用——网络 ElementBank 字段的新数据源）。</summary>
        public int GetAvailableManaCount(ManaType type, Player player)
        {
            var pool = GetPool(player);
            return pool.TempElements.ContainsKey(type) ? pool.TempElements[type] : 0;
        }

        public int GetTotalAvailableMana(Player player)
        {
            return GetPool(player).TempElements.Values.Sum();
        }

        /// <summary>未横置地牌中数量最多的同色数（2026-10-11 重定义——元素充盈引擎判据：
        /// 旧「bank 最多色」随 bank 退役；并列取枚举序靠前者）。</summary>
        public int GetMaxManaCount(Player player)
        {
            var counts = new Dictionary<ManaType, int>();
            foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                counts[m] = 0;
            foreach (var pc in GetPool(player).PooledCards)
                if (!pc.IsTapped) counts[pc.LockedColor]++;
            int best = 0;
            foreach (var kv in counts)
                if (kv.Value > best) best = kv.Value;
            return best;
        }

        public List<PooledCard> GetPooledCards(Player player)
        {
            return GetPool(player).PooledCards.ToList();
        }

        // ======================================== 重置/工具 ========================================

        public void Reset()
        {
            foreach (var pool in _playerPools.Values)
            {
                pool.PooledCards.Clear();
                pool.Curve = null;
                pool.GlobalTurnIndex = 0;
                pool.PersonalTurnIndex = 0;
                pool.TapsThisTurn = 0;
                pool.BlackGainedThisTurn = 0;
                pool.WhiteGainedThisTurn = 0;
                pool.LandCapBonus = 0;
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                {
                    pool.TempElements[mana] = 0;
                }
            }
        }

        public string GetStatusString(Player player)
        {
            var pool = GetPool(player);
            var result = $"元素池 (地牌 {pool.PooledCards.Count}/{GetLandCap(player)}):\n";

            foreach (var pc in pool.PooledCards)
            {
                var name = (pc.SourceCard as IHasName)?.CardName ?? pc.SourceCard.ID;
                var tapStr = pc.IsTapped ? "（已横置）" : "";
                result += $"  [{name}]{tapStr} 锁色: {pc.LockedColor}\n";
            }

            var walletStr = string.Join(", ", pool.TempElements.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
            result += $"临时钱包(本回合): {walletStr}\n";
            result += $"黑白获得(本回合): 黑{pool.BlackGainedThisTurn}/白{pool.WhiteGainedThisTurn}\n";
            result += $"本回合系统横置: {pool.TapsThisTurn}";

            return result;
        }

        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
        }
    }

    // ======================================== 元素池事件 ========================================

    public class ElementPoolAddEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card AddedCard { get; set; }
        /// <summary>入池锁定的产色（2026-10-11：指示物 Tokens 退役，改单色字段）。</summary>
        public ManaType LockedColor { get; set; }
    }

    /// <summary>元素入账来源：缺省=效果发放（AddMana 临时钱包路径）。
    /// Tap 位保留枚举序兼容——付费系统横置不发 GainEvent（实付组合走 ElementPoolPayEvent.PaidCost）。</summary>
    public enum GainSource
    {
        /// <summary>效果发放（代价补偿/错边/光合作用/生物特性产色等 AddMana 路径）——缺省值</summary>
        Grant = 0,
        /// <summary>地牌横置产出（2026-10-11 退役：现取现用，横置直接支付不发本事件）</summary>
        Tap = 1,
    }

    public class ElementPoolGainEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ManaType GainedType { get; set; }
        public Card FromCard { get; set; }
        /// <summary>本次产出数量。</summary>
        public int Amount { get; set; } = 1;
        /// <summary>入账来源：缺省 Grant（临时钱包路径）。</summary>
        public GainSource Source { get; set; } = GainSource.Grant;
    }

    public class ElementPoolPayEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Dictionary<int, float> PaidCost { get; set; }
        /// <summary>支付来源说明（英雄技能/打出卡名/效果费）——仅供战报渲染，不参与规则判定。
        /// PaidCost=实付组合（临时钱包+系统横置地牌合计口径）。</summary>
        public string SourceNote { get; set; }
    }

    /// <summary>
    /// 资源曲线变更事件（设置/变速效果修改曲线时发布；成长原子 +1 也经此通知）。
    /// 经 GameCore 统一路由，Trigger/Layer 引擎可见。
    /// </summary>
    public class CurveShiftEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ResourceCurve NewCurve { get; set; }
        /// <summary>变更原因（InitGame / 成长 / 变速效果 ID 等，调试用）</summary>
        public string Reason { get; set; }
    }

    // 保留旧事件名的兼容性
    public class ElementPoolConsumeEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ManaType ManaType { get; set; }
        public int Amount { get; set; }
        public Effect Source { get; set; }
    }
}
