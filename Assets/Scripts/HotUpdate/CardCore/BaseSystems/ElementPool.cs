using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 元素池中的卡牌（地牌，锁色产色体）——2026-10-11 资源系统重做定案：
    /// 指示物整套退役。入池时从锁色候选（卡面/各模式费 ∪ 主动效果锚价的红/蓝/绿/灰——
    /// 见 ElementPoolSystem.GetLandColorChoices）中自选锁定一个产色，后续生成固定；
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

        /// <summary>冻地层数（2026-10-29 阶段3a 冻结扩展到元素池）：层=剩余回合数——冻结原子横置地牌
        /// 并叠层（不挂指示物——地牌不是 Entity）；持有者回合末 −1，层&gt;0 时回合开始不解除横置，
        /// 归零后的下个回合开始恢复。叠加=延长（与单位冻结同口径）。</summary>
        public int FreezeLayers { get; set; }

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

        /// <summary>最近的全局回合数（2026-10-15 定案：地牌槽曲线 CapAt 的索引回归全局口径——
        /// 每次回合开始全体池同步推进，双方上限数值恒等，先后手无差）</summary>
        public int GlobalTurnIndex { get; set; }

        /// <summary>个人回合数（自己回合开始时 +1；纯台账/观测用——ResourceLedger 行标识）</summary>
        public int PersonalTurnIndex { get; set; }

        /// <summary>「成长」原子累积的地牌上限加值（2026-10-15 定案：无封顶——自然上限 10 之外
        /// 的增加全部走此加值，效果叠加不设限；各自池生效不共享）。</summary>
        public int LandCapBonus { get; set; }

        /// <summary>本回合系统横置次数（付费自动横置；每个全局回合重置——纯观测）</summary>
        public int TapsThisTurn { get; set; }

        public PlayerElementPool()
        {
            foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
            {
                TempElements[mana] = 0;
            }
        }
    }

    /// <summary>
    /// 元素池系统——2026-10-11 资源系统重做定案（现取现用）+ 2026-10-15 上限累加口径修订：
    /// 1. 地牌 = 锁色产色体：入池时自选卡费颜色之一锁定；不消耗不耗竭；移除口=摧毁/抹除效果。
    /// 2. 地牌上限 = 每次回合开始双方同步 +1（全局曲线 CapAt(GlobalTurnIndex)，标准曲线 min(全局回合,10)）：
    ///    2026-10-11 的个人口径废止——先后手永差一步的观感过强，回归双方同步（10-11 前口径）；
    ///    自然上限 10（LandCurveCap），「成长」原子 +1/次**无封顶**（旧 14 硬顶退役）。
    ///    各自上限管各自地牌张数（10-10 的全场合计占用不复活）。
    /// 3. 付费 = 现取现用：按账单规划「临时钱包余额 + 未横置地牌横置」组合，钱包优先消耗（回合末即逝），
    ///    地牌横置=系统自动的强制动作（无速度交互），自己/对手回合都一样（跨回合支付不变）。
        /// 4. 支付链（2026-10-28 四色重构）：红/蓝/绿仅本色；灰=灰→红→蓝→绿（黑白删除后不再垫灰，
        ///    无需先行预留）；黑白等非四色需求沿本色链扣（过渡态，P2 删枚举成员后不再出现）；
        ///    **任意元素（Any）=万能货币**（42e883b2 元素合成生成、落临时钱包），恒处各链末位垫付
        ///    ——可代替红蓝绿灰缺口（本色优先消耗）。浓度上限
    ///    （2026-10-16 临时元素超上限改版）：只钳**地牌横置贡献**（每货币单次 ≤ 地牌上限——地牌张数
    ///    本就有界，钳为双保险），临时钱包贡献不受钳（获取=对应地块可用+1、可用可超上限——
    ///    纯色需求超帽部分由钱包覆盖即付）。
        /// 5. 临时钱包：效果授予元素的唯一归宿，任一回合结束清空；生成无上限（2026-10-28 P3b 定案：
        ///    全类型累计钳删除——代价已不产元素；2026-10-30 起效果无每回合次数上限，
        ///    发电机节奏由卡自身时机期望系数与付费约束承担）。
    /// 6. 黑白不由地牌产出（锁定色不含黑白）；获取通道=代价补偿/错边转化等卡结算路径。
    /// 7. 发动无效与地牌零交互（净零靠付费在响应窗之后，本就无退款路径）；地牌的交互面只有摧毁/抹除。
    /// </summary>
    public class ElementPoolSystem
    {
        /// <summary>地牌槽上限自然封顶（曲线末端值；2026-10-13 迁表：=GameRules.LandCurveCap 同源单值；
        /// 2026-10-15 缩盘口径 7→10——效果增加（成长）在此之外无限制叠加）</summary>
        public static int MAX_POOL_SIZE
            => (int)ValueSystemConfigManager.Instance.GetOrCreateConfig().GameRulesConfig.LandCurveCap;

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
        ///（否则副本成免费地牌，2026-10-11 维持禁令）。
        /// 锁定卡（LockCounter&gt;0）不可——锁定=无法进入地牌区，放地不得绕道封锁（2026-10-13 文案定案）。
        /// UI/AI 预检与 AddCardToPool 权威校验共用此判据。
        /// </summary>
        public static bool CanServeAsLand(Card card)
            => card is CardWrapper
               && card.GetCounterCount(Attribute.CounterRules.TempCounter) <= 0 // 临时复制卡（临时指示物标记）
               && card.GetCounterCount(Attribute.CounterRules.LockCounter) <= 0 // 锁定：无法进入地牌区（放地绕道封锁）
               && !card.IsToken
               && card is IHasSupertype ht
               && (ht.Supertype == Cardtype.Creature
                   || ht.Supertype == Cardtype.Spell
                   || ht.Supertype == Cardtype.Enchantment);

        /// <summary>
        /// 地牌可锁定的产色候选（2026-10-23 锚价开格定案，取代「仅卡面费取色」旧口径）：
        /// （卡面/各模式推导费 ∪ 全部主动效果锚价）中的红/蓝/绿/灰颜色集合（黑白不由地牌产出）。
        /// 正形法术的主动效果构筑期不占卡费（卡面费 0），元素需求全在锚价——按锚价开格；
        /// 抉择类法术（1~3 主动效果）逐效果锚价并集=多色多地格同开；抉择永久物各模式费并入。
        /// 按份额降序排列（并列取枚举序靠前者）——缺省锁色（colorChoice=null）取首位=主色。
        /// 空候选（全无费用元素——真无费/纯黑白）不可入池。
        /// 结果缓存于 CardData.LandColorChoicesCache（ResetCache 失效）；返回副本。
        /// </summary>
        public static List<ManaType> GetLandColorChoices(Card card)
            => GetLandColorChoices(card is CardWrapper wrapper ? wrapper.GetData() : null);

        /// <summary>同 GetLandColorChoices（CardData 重载——网络侧无引擎对象时直用卡数据，单源）。</summary>
        public static List<ManaType> GetLandColorChoices(CardData data)
        {
            if (data == null) return new List<ManaType>();
            if (data.LandColorChoicesCache != null) return new List<ManaType>(data.LandColorChoicesCache);

            var total = new ElementCost();
            void Add(ElementCost cost)
            {
                if (cost == null) return;
                foreach (var color in cost.NonzeroColors())
                    total[color] = total[color] + cost[color];
            }

            // ① 卡面/各模式推导费（抉择卡逐模式并集——不再只读最大模式费显示缓存）
            if (CostDerivationService.HasChoiceEffect(data))
            {
                int modeCount = CostDerivationService.GetModeCount(data);
                for (int m = 0; m < modeCount; m++)
                    Add(CardCostService.GetModeCost(data, m));
            }
            else
                Add(CardCostService.GetRuntimeCost(data, 0));

            // ② 主动效果锚价（效果真正需要的元素——构筑期不占卡费、运行时现付，口径同 SpellCastAnchor）：
            //    逐主动效果 ×（含抉择步时）逐模式并入
            int anchorModes = Math.Max(1, CostDerivationService.GetModeCount(data));
            foreach (var def in CardEffectConverter.ConvertAll(data.Effects, data.ID))
            {
                if (def == null || !def.IsActivatedEffect) continue;
                for (int m = 0; m < anchorModes; m++)
                    Add(CostDerivationService.DeriveElementCosts(def, m));
            }

            var pairs = new List<(ManaType color, int amount)>();
            foreach (var type in total.NonzeroColors())
            {
                // 支付进1口径（2026-10-30）：0.5 锚价色对战也要付 1——不再 (int) 截断丢色，按向上取整入排序
                int amount = (int)Math.Ceiling(total[type] - 1e-4f);
                if (amount > 0 && type != ManaType.Any) // 白/黑席位已删（2026-10-28 P2），无需再滤
                    pairs.Add((type, amount));
            }
            var result = pairs
                .OrderByDescending(p => p.amount)
                .ThenBy(p => (int)p.color)
                .Select(p => p.color)
                .ToList();
            data.LandColorChoicesCache = result;
            return new List<ManaType>(result);
        }

        /// <summary>入池预检（UI/AI 共用单点）：资格由 CanServeAsLand 另查；这里看禁地令与各自上限余位。</summary>
        public bool CanAddLand(Player owner)
            => !RuleAuraSystem.ScopeHits(RuleAuraComponents.NoLandAura, owner) && GetPool(owner).PooledCards.Count < GetLandCap(owner);

        /// <summary>
        /// 将手牌作为地牌放入元素池（主阶段调用，可用/未横置状态入场，不限张数/次数）。
        /// 张数受**各自**地牌槽上限约束（2026-10-15：全局同步曲线 min(全局回合,10)+成长加值，效果增加无封顶）。
        /// colorChoice：从 GetLandColorChoices 中自选锁定产色；null=缺省主色（份额最多色）。
        /// 地牌不消耗不耗竭；离池（弹回手等）后可再入池重新选色。
        /// </summary>
        public bool AddCardToPool(Card card, Player owner, ManaType? colorChoice = null)
        {
            if (card == null || owner == null) return false;

            // 禁地令（2026-10-29）：受光环影响的一方不能将手牌作为地牌放置（权威校验，UI 预检之外兜底）
            if (RuleAuraSystem.ScopeHits(RuleAuraComponents.NoLandAura, owner)) return false;

            // 地牌资格：卡组正式三型（衍生物/临时复制卡不可）
            if (!CanServeAsLand(card))
                return false;

            var pool = GetPool(owner);

            // 地牌槽上限（各自口径）
            if (pool.PooledCards.Count >= GetLandCap(owner))
                return false;

            // 锁色候选（卡面/各模式费 ∪ 主动效果锚价，去黑白）；空候选（真无费用元素）拒绝入池
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
        /// 「成长」原子（2026-10-15 定案：无封顶）：效果控制者地牌上限 +1，
        /// 各自池生效不共享、可无限重复使用（自然上限 10 之外的增加全走此口）。
        /// 作用于 GetLandCap 单点：入池闸/支付浓度/费用门槛/黑白钳/AI/快照同随。
        /// </summary>
        public bool AddLandCapBonus(Player player)
        {
            if (player == null) return false;
            var pool = GetPool(player);
            pool.LandCapBonus++;
            PublishRouted(new CurveShiftEvent
            {
                Player = player,
                NewCurve = pool.Curve, // 曲线对象未换——事件仅作 Trigger/Layer 可见的通知载体
                Reason = "成长:地牌上限+1",
            });
            return true;
        }

        /// <summary>
        /// 当前的地牌槽上限（2026-10-15 定案：**全局同步曲线**——每次回合开始双方同步 +1，
        /// 读 GlobalTurnIndex；双方数值恒等，先后手无差。10-11 个人口径与 10-10 全场共享占用双双废止）。
        /// 标准曲线 = min(全局回合数, 10)；成长原子加值 LandCapBonus 无封顶。
        /// 无曲线时按同一公式兜底。挑战模式 P2 加成曲线照常各自注入（全局索引）。
        /// </summary>
        public int GetLandCap(Player player)
        {
            var pool = GetPool(player);
            int cap;
            if (pool.Curve != null)
                cap = pool.Curve.CapAt(pool.GlobalTurnIndex);
            else
                cap = Math.Max(1, Math.Min(pool.GlobalTurnIndex, MAX_POOL_SIZE));
            return cap + pool.LandCapBonus;
        }

        // ======================================== 临时钱包（效果授予元素） ========================================

        /// <summary>效果发放（代价补偿/错边/光合作用/带地牌特性生物产色等 AddMana 路径）——
        /// 落**临时钱包**（任一回合结束清空）。2026-10-28 P3b 定案：全类型累计钳删除——
        /// 生成不再受每回合总量上限，实发量=请求量。</summary>
        public int AddMana(Player player, ManaType type, Card fromCard, int amount = 1)
        {
            if (player == null || amount <= 0) return 0;
            var pool = GetPool(player);

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

        // ======================================== 支付（现取现用统一规划器） ========================================

        /// <summary>支付规划结果：钱包扣减 + 横置地牌清单 + 实付组合（事件用）。</summary>
        private sealed class PaymentPlan
        {
            public readonly Dictionary<ManaType, int> WalletUse = new Dictionary<ManaType, int>();
            public readonly List<PooledCard> Taps = new List<PooledCard>();
            public readonly Dictionary<ManaType, int> TotalPaid = new Dictionary<ManaType, int>();
        }

        /// <summary>
        /// 支付预检（声明期门禁用，纯函数不动状态）：临时钱包加未横置地牌能否覆盖账单
        ///（代价补偿黑/白不虚拟并入——2026-10-16 恢复旧序，补偿垫不了本卡）。
        /// </summary>
        public bool CanPayCostWithAutoTap(ElementCost cost, Player player)
            => CanPayCostWithAutoTap(ElementPaymentValidator.NormalizeBill(cost), player);

        /// <summary>同 CanPayCostWithAutoTap（已归一账单入口——GameActions.CanAfford 合并 pending 声明承诺后调用）。</summary>
        public bool CanPayCostWithAutoTap(Dictionary<ManaType, int> bill, Player player)
        {
            var pool = GetPool(player);
            return PlanBill(bill, SnapshotWallet(pool), UntappedLands(pool), GetLandCap(player)) != null;
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
        /// ① 需求侧浓度上限已退役（2026-10-16 临时元素超上限改版）：纯色需求超帽不再直接拒付——
        ///    钱包贡献不受浓度钳，能否覆盖由规划自身判定（地牌贡献仍每货币 ≤ cap，天然有界）；
        /// ② 按 FourColorOrder×ChainFor 贪心扣减（支付链 2026-10-28 四色重构：三色仅本色；
        ///    灰=灰→红→蓝→绿——黑白不再垫灰、无需先行预留）；
        /// ③ 黑白等非四色需求（P2 删枚举成员前过渡态）垫后沿本色链扣（只来自钱包——地牌锁色不含黑白）；
        /// ④ 同货币先扣钱包（临时元素回合末即逝，优先消耗，**不受浓度钳**——可用可超上限）再横置地牌
        ///    （地牌每货币单次贡献 ≤ cap，双保险钳）。
        /// </summary>
        private static PaymentPlan PlanBill(
            Dictionary<ManaType, int> bill, Dictionary<ManaType, int> wallet,
            List<PooledCard> untapped, int cap)
        {
            if (bill == null || bill.Count == 0) return new PaymentPlan();

            var walletSim = new Dictionary<ManaType, int>();
            var landsOf = new Dictionary<ManaType, Queue<PooledCard>>();
            var used = new Dictionary<ManaType, int>();
            var landUsed = new Dictionary<ManaType, int>(); // 地牌贡献台账（浓度钳只管地牌——钱包豁免）
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
            {
                walletSim[c] = wallet.TryGetValue(c, out var v) ? v : 0;
                used[c] = 0;
                landUsed[c] = 0;
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
                    landUsed[currency] += take;
                    plan.Taps.Add(land);
                }
            }

            // ② 四色需求按固定序×填链贪心：钱包优先（不受浓度钳，可超上限）、后横置地牌（每货币 ≤ cap）
            foreach (var need in ElementPaymentValidator.FourColorOrder)
            {
                if (!bill.TryGetValue(need, out var remaining) || remaining <= 0) continue;
                foreach (var currency in ElementPaymentValidator.ChainFor(need))
                {
                    if (remaining <= 0) break;
                    int have = walletSim[currency];
                    int take = Math.Min(have, remaining); // 钱包：无浓度钳（2026-10-16 超上限改版）
                    if (take > 0)
                    {
                        Record(currency, take, null);
                        remaining -= take;
                    }
                    if (remaining > 0 && landsOf.TryGetValue(currency, out var queue))
                    {
                        int room = cap - landUsed[currency]; // 地牌：浓度钳只计地牌贡献
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

            // ③ 黑白等非四色需求（过渡态）：沿各自填链（本色）扣钱包——灰链已不含黑白，
            //    四色贪心动不到黑白货币，无需先行预留；地牌锁色不含黑白，横置侧天然不参与。
            foreach (var need in bill.Keys)
            {
                if (Array.IndexOf(ElementPaymentValidator.FourColorOrder, need) >= 0) continue;
                if (!bill.TryGetValue(need, out var remaining) || remaining <= 0) continue;
                foreach (var currency in ElementPaymentValidator.ChainFor(need))
                {
                    if (remaining <= 0) break;
                    int take = Math.Min(walletSim[currency], remaining);
                    if (take <= 0) continue;
                    Record(currency, take, null);
                    remaining -= take;
                }
                if (remaining > 0) return null;
            }

            return plan;
        }

        // ======================================== 回合管理 ========================================

        /// <summary>
        /// 全局回合开始（GameCore.OnTurnStarted 调用，每回合一次，无论轮到谁）：
        /// 1. 全体池 GlobalTurnIndex 同步推进（地牌上限曲线随之双方同步 +1——2026-10-15 全局口径）；
        ///    回合玩家个人回合数 +1（纯台账）
        /// 2. 回合玩家（准备阶段）地牌全部解除横置
        /// 返回值=本次解横置的地牌张数（2026-10-26 战报重置账目：StandbyResetEvent 数据源）。
        /// </summary>
        public int OnTurnStart(Player turnPlayer, int globalTurnNumber)
        {
            if (turnPlayer == null) return 0;

            var turnPool = GetPool(turnPlayer);
            turnPool.PersonalTurnIndex++;

            foreach (var pool in _playerPools.Values)
            {
                if (globalTurnNumber > pool.GlobalTurnIndex)
                    pool.GlobalTurnIndex = globalTurnNumber;
                pool.TapsThisTurn = 0;
            }

            int untapped = 0;
            foreach (var pc in turnPool.PooledCards)
                if (pc != null && pc.IsTapped && pc.FreezeLayers <= 0) { pc.IsTapped = false; untapped++; }
            return untapped;
        }

        /// <summary>
        /// 回合结束（GameCore.OnTurnEnded 调用）：**临时钱包清空**（2026-10-11 定案：任一回合结束，
        /// 未消耗的效果授予元素即逝——现取现用）。地牌无耗竭清理（旧自动产色/耗尽送墓已随指示物退役）。
        /// 冻地衰减（2026-10-29 阶段3a）：**持有者**回合末层 −1（与单位冻结的持有者侧结算域同口径）
        /// ——归零后的下个回合开始恢复横置（OnTurnStart 的层&gt;0 不重置闸）。
        /// </summary>
        public void OnTurnEnd(Player turnPlayer)
        {
            if (turnPlayer == null) return;
            foreach (var pool in _playerPools.Values)
            {
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                    pool.TempElements[mana] = 0;
            }

            var holderPool = GetPool(turnPlayer);
            foreach (var pc in holderPool.PooledCards)
                if (pc != null && pc.FreezeLayers > 0) pc.FreezeLayers--;
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

        /// <summary>元素池剩余可付总量（2026-10-20 判据五恢复——元素充盈引擎判据）：全六色合计
        /// 「未横置地牌张数 + 临时钱包余额」，与出牌支付同资源口径（钱包优先消耗+地牌横置补足）。
        /// 引擎在对手施放结算后读双方此值求差（判据三同式，判据四「上限差」随改案退役）。</summary>
        public int GetRemainingElementTotal(Player player)
        {
            var pool = GetPool(player);
            int wallet = pool.TempElements.Values.Sum();
            int untapped = pool.PooledCards.Count(pc => pc != null && !pc.IsTapped);
            return wallet + untapped;
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

    public class ElementPoolGainEvent : GameEventBase
    {
        public Player Player { get; set; }
        public ManaType GainedType { get; set; }
        public Card FromCard { get; set; }
        /// <summary>本次产出数量。</summary>
        public int Amount { get; set; } = 1;
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
