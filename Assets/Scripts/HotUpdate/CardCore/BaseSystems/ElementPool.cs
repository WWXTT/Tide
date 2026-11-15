using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 元素池中的卡牌（地牌，产色体）——2026-11-15 地牌改版（单格放置·多色产色）：
    /// 放置不再预选锁色。入池时把可产色候选（卡面/各模式费 ∪ 主动效果锚价的红/蓝/绿/灰——
    /// 见 ElementPoolSystem.GetLandColorChoices）整体**快照**成 ColorOptions；之后一次横置
    /// 只产 **1 个**元素，颜色 ∈ ColorOptions（自动付费由规划器按账单选定，手动产色由玩家选）。
    /// 地牌不消耗、不耗竭、不入墓（移除口=摧毁/抹除效果）；每回合周期至多横置一次。
    /// </summary>
    public class PooledCard
    {
        public Card SourceCard { get; }

        /// <summary>可产色集合（2026-11-15）：放置瞬间 GetLandColorChoices 的快照（按份额降序，
        /// 首位=主色）——不随之后的费用增减/锚价变化重推导；空集合=非法态（正常入口不会产生）。</summary>
        public List<ManaType> ColorOptions { get; }

        /// <summary>展示色（= ColorOptions 首位主色）：仅供面片底色/棋盘落格/战报展示，
        /// **不限制产色**（付费规划与手动产色按集合匹配）。</summary>
        public ManaType DisplayColor => ColorOptions != null && ColorOptions.Count > 0
            ? ColorOptions[0] : ManaType.Gray;

        /// <summary>该地牌能否产 color——按色地牌计数（禁召/归土/纳川/代价门槛/单色可用性）
        /// 与支付匹配的**单源谓词**（2026-11-15 决策3：多色地同时计入多个颜色）。</summary>
        public bool CanProduce(ManaType color)
            => ColorOptions != null && ColorOptions.Contains(color);

        /// <summary>本回合周期已横置（已为某次支付/手动产色贡献过 1 个元素）。己方回合开始时恢复（解除横置）。</summary>
        public bool IsTapped { get; set; }

        /// <summary>冻地层数（2026-10-29 阶段3a 冻结扩展到元素池）：层=剩余回合数——冻结原子横置地牌
        /// 并叠层（不挂指示物——地牌不是 Entity）；持有者回合末 −1，层&gt;0 时回合开始不解除横置，
        /// 归零后的下个回合开始恢复。叠加=延长（与单位冻结同口径）。</summary>
        public int FreezeLayers { get; set; }

        /// <summary>正常入口构造（可产集合快照；DisplayColor=首位主色）。</summary>
        public PooledCard(Card card, IEnumerable<ManaType> colorOptions)
        {
            SourceCard = card;
            ColorOptions = colorOptions != null
                ? new List<ManaType>(colorOptions) : new List<ManaType>();
        }

        /// <summary>单色构造（旧快照恢复兜底 / 夹具 forced 色专用——完全复刻旧锁色行为）。</summary>
        public PooledCard(Card card, ManaType singleColor)
        {
            SourceCard = card;
            ColorOptions = new List<ManaType> { singleColor };
        }

        /// <summary>旧锁色口径（2026-11-15 退役）：等价于 DisplayColor（展示色）。
        /// 仅为编译期找齐残留调用点保留的别名——序列化/网络字段名暂不改（协议兼容）。</summary>
        [Obsolete("2026-11-15 地牌改版：锁色退役——展示用 DisplayColor，产色判定用 CanProduce(color)")]
        public ManaType LockedColor => DisplayColor;
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
    /// 元素池系统——2026-10-11 资源系统重做（现取现用）+ 2026-11-15 地牌改版（单格放置·多色产色）：
    /// 1. 地牌 = 产色体：入池**不再选色**——可产集合=放置瞬间 GetLandColorChoices 快照（ColorOptions），
    ///    一次横置只产 1 个元素、颜色 ∈ 集合（自动付费由规划器选，手动产色由玩家选）；
    ///    不消耗不耗竭；移除口=摧毁/抹除效果。
    /// 2. 地牌上限 = 每次回合开始双方同步 +1（全局曲线 CapAt(GlobalTurnIndex)，标准曲线 min(全局回合,10)）；
    ///    自然上限 10（LandCurveCap），「成长」原子 +1/次**无封顶**。各自上限管各自地牌张数。
    /// 3. 付费 = 现取现用：最小费用流一次规划「钱包扣减 + 地牌横置（地→色匹配）」——
    ///    **钱包全链优先于任何横置**（手动预生成不白做）、横置张数最少、本色优先（链序罚分平局）；
    ///    地牌横置=系统自动的强制动作（无速度交互），自己/对手回合都一样。
    /// 4. 支付链（2026-10-28 四色重构沿用）：红/蓝/绿=[本色,任意]；灰=[灰,红,蓝,绿,任意]；
    ///    **彩元素（Any）=万能货币**（效果授予落临时钱包），恒处各链末位垫付。旧浓度上限钳随
    ///    「1 地 1 元素」模型退役（横置数 ≤ 池张数 ≤ 上限，天然有界）；钱包贡献不受钳。
    /// 5. 临时钱包：效果授予/手动产色元素的唯一归宿（**同池同寿命，规则上不可区分**——2026-11-15 决策4），
    ///    任一回合结束清空。
    /// 6. 黑白/任意不由地牌产出；获取通道=代价补偿/错边转化等卡结算路径。
    /// 7. 发动无效与地牌零交互；地牌的交互面只有摧毁/抹除（+主动横置产色=TapLandForElement）。
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
        /// 锁定卡（LockCounter&gt;0）可作地牌放置（2026-11-02 锁定改版：锁定只封「使用」，
        /// 放地不再算绕道封锁——旧 2026-10-13 禁令废止）。
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
        /// 2026-11-15 地牌改版：放置**不再选色**——ColorOptions=放置瞬间 GetLandColorChoices 快照，
        /// DisplayColor=首位主色（仅展示）；产色在横置时按集合选定（自动规划/手动选择）。
        /// 地牌不消耗不耗竭；离池（弹回手等）后可再入池（集合按离池后卡数据重取）。
        /// </summary>
        public bool AddCardToPool(Card card, Player owner)
            => TryAddToPool(card, owner, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：与 AddCardToPool 同路径单源，四闸逐条可读。</summary>
        public bool TryAddToPool(Card card, Player owner, out string reason)
        {
            if (!PassesLandGates(card, owner, out reason, out var pool, out var choices))
                return false;

            pool.PooledCards.Add(new PooledCard(card, choices));

            // 事件发布延后（2026-09-26 时序定案沿用）：入池事件不在此发——调用方完成
            // Zone.ElementPool 区域移动后调 PublishPoolAdd。
            return true;
        }

        /// <summary>夹具专用强制单色入池（2026-11-15 地牌改版 §11）：ColorOptions={forcedColor}——
        /// 完全复刻旧「显式锁色」行为，仅供 TutorialScenarioSeeder / TestStartFlow 等教学·测试预设
        /// 保住「四色素材地 10/10/10/10」等局面；**正常 UI/AI/网络入口禁用**（语义=该地只能产这一色）。</summary>
        public bool AddCardToPoolForced(Card card, Player owner, ManaType forcedColor)
            => TryAddToPoolForced(card, owner, forcedColor, out _);

        /// <summary>带原因版：夹具强制色重载（forcedColor 必须在该卡可产候选内——传错即拒，同旧锁色闸）。</summary>
        public bool TryAddToPoolForced(Card card, Player owner, ManaType forcedColor, out string reason)
        {
            if (!PassesLandGates(card, owner, out reason, out var pool, out var choices))
                return false;
            if (!choices.Contains(forcedColor))
            { reason = "所选颜色不在该卡可产的产色内"; return false; }

            pool.PooledCards.Add(new PooledCard(card, forcedColor));
            return true;
        }

        /// <summary>入池四闸单源（禁地令/资格/上限余位/可产候选非空）——正常与 forced 重载共用。</summary>
        private bool PassesLandGates(Card card, Player owner, out string reason,
            out PlayerElementPool pool, out List<ManaType> choices)
        {
            reason = null;
            pool = null;
            choices = null;
            if (card == null || owner == null) { reason = "内部错误：空参数"; return false; }

            // 禁地令（2026-10-29）：受光环影响的一方不能将手牌作为地牌放置（权威校验，UI 预检之外兜底）
            if (RuleAuraSystem.ScopeHits(RuleAuraComponents.NoLandAura, owner))
            { reason = "禁地令生效中——不能放置地牌"; return false; }

            // 地牌资格：卡组正式三型（衍生物/临时复制卡不可）
            if (!CanServeAsLand(card))
            { reason = "该卡不可作为地牌（衍生物/临时复制卡）"; return false; }

            pool = GetPool(owner);

            // 地牌槽上限（各自口径）
            if (pool.PooledCards.Count >= GetLandCap(owner))
            { reason = $"地牌槽已满（上限 {GetLandCap(owner)}）"; return false; }

            // 可产色候选（卡面/各模式费 ∪ 主动效果锚价，去黑白）；空候选（真无费用元素）拒绝入池
            choices = GetLandColorChoices(card);
            if (choices.Count == 0)
            { reason = "该卡无元素费用，不能作为地牌"; return false; }
            return true;
        }

        /// <summary>发布地牌入池事件：调用方在 Zone.ElementPool 区域移动完成后调用
        /// （AddCardToPool 本体不发——见其尾注；LockedColor 字段名保留=协议兼容，语义=展示色）。</summary>
        public void PublishPoolAdd(Card card, Player owner)
        {
            var pooled = GetPool(owner).PooledCards.FirstOrDefault(pc => pc.SourceCard == card);
            PublishEvent(new ElementPoolAddEvent
            {
                Player = owner,
                AddedCard = card,
                LockedColor = pooled?.DisplayColor ?? ManaType.Gray,
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
            int capBefore = GetLandCap(player); // 事件收口（2026-11-13 G8）：战报「上限 x→y」须旧值——改曲线前定格
            pool.Curve = curve;

            PublishRouted(new CurveShiftEvent
            {
                Player = player,
                NewCurve = curve,
                OldCap = capBefore,
                NewCap = GetLandCap(player),
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
            int capBefore = GetLandCap(player); // 事件收口（2026-11-13 G8）：上限 x→y 旧值定格
            pool.LandCapBonus++;
            PublishRouted(new CurveShiftEvent
            {
                Player = player,
                NewCurve = pool.Curve, // 曲线对象未换——事件仅作 Trigger/Layer 可见的通知载体
                OldCap = capBefore,
                NewCap = GetLandCap(player),
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

        /// <summary>支付规划结果：钱包扣减 + 横置地牌清单（含本次产出色）+ 实付组合（事件用）。
        /// internal=PaymentPlanView（公开只读观测口）的构造参数。</summary>
        internal sealed class PaymentPlan
        {
            public readonly Dictionary<ManaType, int> WalletUse = new Dictionary<ManaType, int>();
            public readonly List<(PooledCard Land, ManaType Color)> Taps = new List<(PooledCard, ManaType)>();
            public readonly Dictionary<ManaType, int> TotalPaid = new Dictionary<ManaType, int>();
        }

        /// <summary>支付规划只读视图（2026-11-15 地牌改版）：验证器/测试观测用——
        /// 与 TryPayBillWithAutoTap 同源单规划（PlanBill），不动状态。</summary>
        public sealed class PaymentPlanView
        {
            public IReadOnlyDictionary<ManaType, int> WalletUse { get; }
            /// <summary>横置的地牌（池序）与本次产出色（多色地的规划选色）逐项对应。</summary>
            public IReadOnlyList<(PooledCard Land, ManaType Color)> Taps { get; }
            public IReadOnlyDictionary<ManaType, int> TotalPaid { get; internal set; }

            internal PaymentPlanView(PaymentPlan plan)
            {
                WalletUse = new Dictionary<ManaType, int>(plan.WalletUse);
                Taps = new List<(PooledCard, ManaType)>(plan.Taps);
                TotalPaid = new Dictionary<ManaType, int>(plan.TotalPaid);
            }
        }

        /// <summary>
        /// 支付预检（声明期门禁用，纯函数不动状态）：临时钱包加未横置地牌能否覆盖账单
        ///（代价补偿黑/白不虚拟并入——2026-10-16 恢复旧序，补偿垫不了本卡）。
        /// </summary>
        public bool CanPayCostWithAutoTap(ElementCost cost, Player player)
            => CanPayCostWithAutoTap(ElementPaymentValidator.NormalizeBill(cost), player);

        /// <summary>同 CanPayCostWithAutoTap（已归一账单入口——GameActions.CanAfford 合并 pending 声明承诺后调用）。
        /// 预检与结算同走 PlanBill 单源（2026-11-15 地牌改版：CanPay=true ⇒ Pay 必成功）。</summary>
        public bool CanPayCostWithAutoTap(Dictionary<ManaType, int> bill, Player player)
        {
            var pool = GetPool(player);
            return PlanBill(bill, SnapshotWallet(pool), UntappedLands(pool)) != null;
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
            var plan = PlanBill(bill, SnapshotWallet(pool), UntappedLands(pool));
            if (plan == null) return false;

            foreach (var kv in plan.WalletUse)
                pool.TempElements[kv.Key] -= kv.Value;

            foreach (var tap in plan.Taps)
            {
                tap.Land.IsTapped = true;
                pool.TapsThisTurn++;
            }

            // PaidCost 携带实付组合（钱包+横置合计口径）——三相光环/台账按真实货币计数
            PublishEvent(new ElementPoolPayEvent
            {
                Player = player,
                PaidCost = plan.TotalPaid.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value),
                SourceNote = sourceNote,
            });

            // 元素充盈横置触发口已于 2026-11-06 判据七退役：单口=使用卡牌结算后
            //（BranchEngines.FlushCardCastSettled，连锁终点）——支付横置不再触发充盈评估。

            return true;
        }

        /// <summary>规划只读观测口（验证器/§5.4 用例）：与付费同源（PlanBill 纯函数），不动状态；
        /// 不可付返回 null。供「两次规划逐项一致」「预检=结算」类断言读取 WalletUse/Taps/TotalPaid。</summary>
        public PaymentPlanView PlanPayment(Dictionary<ManaType, int> bill, Player player)
        {
            var pool = GetPool(player);
            var plan = PlanBill(bill, SnapshotWallet(pool), UntappedLands(pool));
            return plan == null ? null : new PaymentPlanView(plan);
        }

        /// <summary>未横置的地牌（本回合周期尚可为支付贡献 1 个可产色元素）。</summary>
        private static List<PooledCard> UntappedLands(PlayerElementPool pool)
            => pool.PooledCards.Where(pc => pc != null && !pc.IsTapped).ToList();

        private static Dictionary<ManaType, int> SnapshotWallet(PlayerElementPool pool)
            => new Dictionary<ManaType, int>(pool.TempElements);

        // ===== 最小费用流支付规划（2026-11-15 地牌改版 §5）=====
        // 旧「按 LockedColor 单色分桶 + 逐链贪心」在多色地下有两个结构性缺陷：
        //   A. 一块多色地不能同时进多个单色队列（一地一回合只产 1 点，须做地→色匹配）；
        //   B. 跨色链不是钱包优先（灰需求会先横灰地，把红钱包留到回合末清空——手动预产色白做）。
        // 改为一次二部匹配/最小费用流：
        //   源 S → 钱包货币节点 W_c（容量=余额，费 0） / 地牌节点 L_i（容量=1——每地一回合一次）
        //   W_c → 需求 N_d：c ∈ ChainFor(d)，费用=链序（0=本色，越大越靠后）
        //   L_i → 产色节点 C_c（c ∈ ColorOptions_i，容量 1，费用=BIG）→ N_d：c ∈ ChainFor(d)，费用=链序
        //   N_d → T 容量=bill[d]
        // 总费用 = BIG×横置张数 + Σ链序 ⇒ 优先级：横置张数最少 > 本色优先（钱包/地牌同链序口径）。
        // 天然满足：钱包全链优先于任何横置、多色地只被分配一个色、经 C_c 可回读本次产出色写进台账。
        // 确定性：节点/边按固定序建（ManaType 枚举序/池序/FourColorOrder），SPFA 平局按建边序——
        // 同一状态两次规划逐项相同（回放/AI 对拍要求）。
        // 规模：节点 ≤ 1+5+40+4+5+1、边 &lt; 200、流量 ≤ 账单总点数——CanAfford 每帧调用无压力。

        /// <summary>横置一次的罚分权重：远大于「账单总点数 × 最大链序」（链序 ≤4、点数 ≤ 上限+钱包，
        /// 10000 保证了同横置张数下链序只做平局，横置张数恒为第一优先级）。</summary>
        private const long TapCostBig = 10000;

        /// <summary>
        /// 账单支付规划（纯函数、非破坏）：求「钱包扣减 + 地牌横置（含产色选配）」组合使账单可付；
        /// null = 无法补足。2026-11-15 地牌改版：旧分桶贪心退役，改最小费用流（见上方算法注释）；
        /// cap 参数随「1 地 1 元素」模型退役（横置数 ≤ 池张数 ≤ 上限，天然有界）。
        /// 非四色需求（任意/过渡态黑白）沿本色链只由钱包覆盖——地牌可产集合不含它们，流网络同构表达。
        /// </summary>
        private static PaymentPlan PlanBill(
            Dictionary<ManaType, int> bill, Dictionary<ManaType, int> wallet,
            List<PooledCard> untapped)
        {
            var plan = new PaymentPlan();
            if (bill == null || bill.Count == 0) return plan;

            // 需求固定序：FourColorOrder 优先，其余按枚举序（任意/过渡态黑白）——确定性建图
            var needs = new List<ManaType>();
            foreach (var n in ElementPaymentValidator.FourColorOrder)
                if (bill.TryGetValue(n, out var v) && v > 0) needs.Add(n);
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
                if (Array.IndexOf(ElementPaymentValidator.FourColorOrder, c) < 0
                    && bill.TryGetValue(c, out var v) && v > 0) needs.Add(c);
            if (needs.Count == 0) return plan;
            int totalDemand = needs.Sum(n => bill[n]);

            // 链序缓存（ChainFor 数组按下标即序）
            var chains = new Dictionary<ManaType, ManaType[]>();
            int Rank(ManaType currency, ManaType need)
            {
                if (!chains.TryGetValue(need, out var chain)) chains[need] = chain = ElementPaymentValidator.ChainFor(need);
                return Array.IndexOf(chain, currency);
            }

            // 节点布局（固定序）：S | 钱包（枚举序）| 地牌（池序）| 产色（FourColorOrder）| 需求 | T
            int node = 0;
            int S = node++;
            var walletNode = new Dictionary<ManaType, int>();
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
                if (wallet.TryGetValue(c, out var wv) && wv > 0)
                    walletNode[c] = node++;
            var landNode = new int[untapped.Count];
            for (int i = 0; i < untapped.Count; i++) landNode[i] = node++;
            var produceNode = new Dictionary<ManaType, int>();
            foreach (var c in ElementPaymentValidator.FourColorOrder) produceNode[c] = node++;
            var needNode = new Dictionary<ManaType, int>();
            foreach (var n in needs) needNode[n] = node++;
            int T = node++;

            var mcf = new MinCostFlow(node);
            var walletEdges = new List<(ManaType c, int idx)>();
            var landEdges = new List<(int land, ManaType c, int idx)>(); // L→C（可产色邻接，费 BIG）

            foreach (var kv in walletNode)
                walletEdges.Add((kv.Key, mcf.AddEdge(S, kv.Value, wallet[kv.Key], 0)));
            for (int i = 0; i < untapped.Count; i++)
                mcf.AddEdge(S, landNode[i], 1, 0);
            foreach (var wc in walletNode)
                foreach (var n in needs)
                {
                    int r = Rank(wc.Key, n);
                    if (r >= 0) mcf.AddEdge(wc.Value, needNode[n], int.MaxValue, r);
                }
            for (int i = 0; i < untapped.Count; i++)
                foreach (var c in ElementPaymentValidator.FourColorOrder)
                    if (untapped[i].CanProduce(c))
                        landEdges.Add((i, c, mcf.AddEdge(landNode[i], produceNode[c], 1, TapCostBig)));
            foreach (var pc in produceNode)
                foreach (var n in needs)
                {
                    int r = Rank(pc.Key, n);
                    if (r >= 0) mcf.AddEdge(pc.Value, needNode[n], int.MaxValue, r);
                }
            foreach (var n in needs)
                mcf.AddEdge(needNode[n], T, bill[n], 0);

            // 增广至满需求（SSP：每步沿最短路增广，得到该流量值的最小费用流）
            if (mcf.Run(S, T, totalDemand) < totalDemand) return null;

            // 回读流：钱包用量 / 地牌横置与本次产出色 / 实付组合
            foreach (var (c, idx) in walletEdges)
            {
                int used = mcf.FlowOf(idx);
                if (used > 0)
                {
                    plan.WalletUse[c] = used;
                    plan.TotalPaid[c] = plan.TotalPaid.TryGetValue(c, out var prev) ? prev + used : used;
                }
            }
            foreach (var (i, c, idx) in landEdges)
            {
                if (mcf.FlowOf(idx) <= 0) continue;
                plan.Taps.Add((untapped[i], c));
                plan.TotalPaid[c] = plan.TotalPaid.TryGetValue(c, out var prev) ? prev + 1 : 1;
            }
            return plan;
        }

        /// <summary>最小费用流（SPFA/Bellman-Ford 连续最短路增广；规模极小，无外部依赖）。
        /// 建边序固定 + 严格松弛 ⇒ 增广路径确定（同输入同结果）。</summary>
        private sealed class MinCostFlow
        {
            private readonly int _n;
            private readonly List<int> _to = new List<int>();
            private readonly List<int> _cap = new List<int>();
            private readonly List<long> _cost = new List<long>();
            private readonly List<int> _cap0 = new List<int>(); // 原始容量（回读流量用）
            private readonly List<List<int>> _g;

            public MinCostFlow(int n)
            {
                _n = n;
                _g = new List<List<int>>(n);
                for (int i = 0; i < n; i++) _g.Add(new List<int>());
            }

            /// <summary>加边（返回边号——供 FlowOf 回读该边实流量）。</summary>
            public int AddEdge(int u, int v, int cap, long cost)
            {
                int idx = _to.Count;
                _g[u].Add(idx);
                _to.Add(v); _cap.Add(cap); _cost.Add(cost); _cap0.Add(cap);
                _g[v].Add(idx + 1);
                _to.Add(u); _cap.Add(0); _cost.Add(-cost); _cap0.Add(0);
                return idx;
            }

            /// <summary>沿最短路逐路增广至 need（或无路可增）：返回实达流量。</summary>
            public int Run(int s, int t, int need)
            {
                int flow = 0;
                while (flow < need)
                {
                    // SPFA 最短路（费用可为负——残量反向边）
                    var dist = new long[_n];
                    var inQueue = new bool[_n];
                    var prevEdge = new int[_n];
                    for (int i = 0; i < _n; i++) { dist[i] = long.MaxValue; prevEdge[i] = -1; }
                    var queue = new Queue<int>();
                    dist[s] = 0;
                    queue.Enqueue(s);
                    inQueue[s] = true;
                    while (queue.Count > 0)
                    {
                        int u = queue.Dequeue();
                        inQueue[u] = false;
                        foreach (var ei in _g[u])
                        {
                            if (_cap[ei] <= 0) continue;
                            int v = _to[ei];
                            if (dist[u] + _cost[ei] >= dist[v]) continue; // 严格松弛：平局保持首见路径
                            dist[v] = dist[u] + _cost[ei];
                            prevEdge[v] = ei;
                            if (!inQueue[v]) { queue.Enqueue(v); inQueue[v] = true; }
                        }
                    }
                    if (dist[t] == long.MaxValue) break;

                    // 沿 prevEdge 回溯找瓶颈（同时受 need − flow 钳）
                    int bottleneck = need - flow;
                    for (int v = t; v != s;)
                    {
                        int ei = prevEdge[v];
                        bottleneck = Math.Min(bottleneck, _cap[ei]);
                        v = _to[ei ^ 1];
                    }
                    for (int v = t; v != s;)
                    {
                        int ei = prevEdge[v];
                        _cap[ei] -= bottleneck;
                        _cap[ei ^ 1] += bottleneck;
                        v = _to[ei ^ 1];
                    }
                    flow += bottleneck;
                }
                return flow;
            }

            /// <summary>边 idx 的实流量（原始容量 − 残量）。</summary>
            public int FlowOf(int idx) => _cap0[idx] - _cap[idx];
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
                result += $"  [{name}]{tapStr} 可产: {string.Join("/", pc.ColorOptions)}\n";
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
        /// <summary>展示色（2026-11-15 地牌改版：字段名保留=协议/回放兼容，语义从「锁定产色」
        /// 改为 ColorOptions 首位主色——仅供面片/脉冲/日志展示，不限制产色；
        /// 实际产出看 ElementPoolGainEvent.GainedType）。</summary>
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
        /// <summary>上限旧值/新值（2026-11-13 G8 战报补全）：发布口定格——战报「地牌槽上限 x→y」；
        /// 建局 InitGame 注入曲线时两值相等（渲染层判 OldCap==NewCap 不出行）。</summary>
        public int OldCap { get; set; }
        public int NewCap { get; set; }
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


