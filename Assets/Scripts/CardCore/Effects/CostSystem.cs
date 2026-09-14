using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    // ================================================================
    // 代价接口与数据
    // ================================================================

    /// <summary>
    /// 代价类型（2026-09-14 代价原子化定案：资源类特殊代价——弃牌/生命/沉睡/送墓/自紊乱/对手增益——
    /// 全部退役，由**代价栏 Payload 原子**承载（流失生命=LifeLoss、弃手牌=DiscardCard、送墓=MillCard
    /// 等，付费步执行 + 错边全价补偿黑/白——见 CostCompensationService）。黑白元素获取通道唯一=卡结算。
    /// 中间枚举值已删但 **Payload 显式保留 =7**（EffectCostEntry 按 int 序列化，防旧档错位）；
    /// 编号整体重排留到训练前统一处理（同 Zone 枚举口径）。
    /// </summary>
    public enum CostType
    {
        /// <summary>元素消耗</summary>
        ElementConsume = 0,
        /// <summary>效果型代价（2026-09-11 定案）：代价栏装载的强制原子效果（如「给对手召唤 30/30 衍生物」、
        /// 「弃自己 2 张手牌」「流失 2 点生命上限」），在付费步执行并按全价补偿黑/白元素。
        /// Payload 字段承载原子。显式 =7：原中段值（DiscardCard/LifePayment/Sleep/MillDeck/
        /// SelfSickness/OpponentBuff）已删，保号防序列化错位。</summary>
        Payload = 7,
    }

    /// <summary>
    /// 代价实例
    /// </summary>
    [Serializable]
    public class CostInstance
    {
        /// <summary>代价类型</summary>
        public CostType Type;

        /// <summary>数值（元素数量/Payload 未用）</summary>
        public int Value;

        /// <summary>法力类型（元素消耗专用）</summary>
        public ManaType ManaType;

        /// <summary>效果型代价的原子（CostType.Payload 专用，2026-09-11）：付费步强制执行并按全价补偿黑/白。</summary>
        public AtomicEffectInstance Payload;
    }

    /// <summary>
    /// 代价执行上下文
    /// </summary>
    public class CostContext
    {
        /// <summary>支付者</summary>
        public Player Payer;

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager;

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool;

        /// <summary>效果来源</summary>
        public Entity Source;
    }

    // ================================================================
    // 代价处理器接口与注册表
    // ================================================================

    /// <summary>
    /// 代价处理器接口
    /// </summary>
    public interface ICostHandler
    {
        /// <summary>处理的代价类型</summary>
        CostType CostType { get; }

        /// <summary>检查是否可以支付</summary>
        bool CanPay(CostInstance cost, CostContext context);

        /// <summary>执行支付</summary>
        void Pay(CostInstance cost, CostContext context);

        /// <summary>获取代价描述</summary>
        string GetDescription(CostInstance cost);
    }

    /// <summary>
    /// 代价处理器注册表
    /// </summary>
    public static class CostHandlerRegistry
    {
        private static readonly Dictionary<CostType, ICostHandler> _handlers
            = new Dictionary<CostType, ICostHandler>();

        public static void Register(ICostHandler handler)
        {
            if (handler != null)
                _handlers[handler.CostType] = handler;
        }

        public static ICostHandler GetHandler(CostType type)
        {
            return _handlers.TryGetValue(type, out var handler) ? handler : null;
        }

        public static bool CanPay(CostInstance cost, CostContext context)
        {
            var handler = GetHandler(cost.Type);
            return handler?.CanPay(cost, context) ?? false;
        }

        public static bool Pay(CostInstance cost, CostContext context)
        {
            var handler = GetHandler(cost.Type);
            if (handler == null) return false;
            if (!handler.CanPay(cost, context)) return false;
            handler.Pay(cost, context);
            return true;
        }

        /// <summary>
        /// 检查所有代价是否都可以支付
        /// </summary>
        public static bool CanPayAll(List<CostInstance> costs, CostContext context)
        {
            return costs.All(c => CanPay(c, context));
        }

        /// <summary>
        /// 支付所有代价（原子：先确认可支付「全部」代价，再逐项支付）。
        /// 遵循 MTG 601.2 的「确定总代价 → 不可撤销地支付」模型：
        /// 任一代价不可支付则整体失败且不支付任何代价，从而避免「部分支付」造成的资源丢失。
        /// （不采用支付后回滚：代价事件一旦发布即可能触发观察者，真正的撤销并不安全。）
        /// </summary>
        public static bool PayAll(List<CostInstance> costs, CostContext context)
        {
            if (costs == null || costs.Count == 0)
                return true;

            // 预检：未能确认可支付全部代价时，不开始任何支付
            if (!CanPayAll(costs, context))
                return false;

            foreach (var cost in costs)
            {
                if (!Pay(cost, context))
                    return false;
            }
            return true;
        }
    }

    // ================================================================
    // 内置代价处理器
    // ================================================================

    /// <summary>
    /// 元素消耗代价处理器
    /// 复用 ElementPoolSystem 的 ConsumeElement / CanConsume
    /// </summary>
    public class ElementConsumeCostHandler : ICostHandler
    {
        public CostType CostType => CostType.ElementConsume;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ElementPool == null || context.Payer == null) return false;
            var costDict = new Dictionary<int, float> { { (int)cost.ManaType, cost.Value } };
            return context.ElementPool.CanPayCost(costDict, context.Payer);
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var costDict = new Dictionary<int, float> { { (int)cost.ManaType, cost.Value } };
            context.ElementPool.PayCost(costDict, context.Payer);
        }

        public string GetDescription(CostInstance cost)
        {
            return $"消耗 {cost.Value} 点{cost.ManaType} 元素";
        }
    }

    // ================================================================
    // 代价相关事件（域事件——发布方为原子 handler，消费方=对局统计/仪式组件）
    // ================================================================
    // 2026-09-14 代价原子化：资源类代价处理器（弃牌/生命/沉睡/送墓/自紊乱/对手增益）随
    // CostOffset 抵消系统退役——资源支付改由**代价栏 Payload 原子**承载（付费步执行 +
    // 错边全价补偿黑/白）。下列域事件保留：改由对应原子 handler 发布——
    // MillCard（磨**自己**牌库=送墓语义）与 LifeLoss（流失**自己**生命上限=支付语义）
    // 在归属==控制者时发布，MatchStatsService / RitualTrackers 等既有订阅零改动。

    /// <summary>送墓（自己牌库）事件：MillCard 原子磨自己牌库时发布（批量）。</summary>
    public class MillDeckCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Cards { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>生命支付事件：LifeLoss 原子流失**自己**生命上限时发布（流失敌方=攻击非支付，不发）。</summary>
    public class LifePaymentCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 效果型代价处理器（2026-09-11 定案）：代价栏装载的强制原子效果（如「给对手召唤 30/30 衍生物」、
    /// 「弃自己 2 张手牌」「流失 2 点生命上限」「磨自己 5 张牌库」）。
    /// CanPay 恒真（强制效果无支付门槛）；实际执行在 CostCompensationService.PayWithCompensationAsync
    /// （付费步异步路径——ICostHandler 接口为同步，此处只做注册占位与描述，防双执行）。
    /// </summary>
    public class PayloadCostHandler : ICostHandler
    {
        public CostType CostType => CostType.Payload;

        public bool CanPay(CostInstance cost, CostContext context)
            => cost?.Payload != null;

        public void Pay(CostInstance cost, CostContext context)
        {
            // 异步执行与补偿在 CostCompensationService（付费步），此处不动
        }

        public string GetDescription(CostInstance cost)
            => cost?.Payload != null ? $"代价效果：{cost.Payload.GetDescription()}" : "代价效果";
    }

    // ================================================================
    // 元素代价支付（2026-09-14：原 CostOffsetService 的元素支付段——兑换面板随抵消系统退役）
    // ================================================================

    /// <summary>
    /// 元素代价支付（纯支付，无兑换）：按颜色聚合需求 → 浓度上限校验 → 扣款 + 支付事件。
    /// 纯色（红/蓝/绿/黑/白）单次支付量 ≤ 支付者地牌槽上限；灰走 ElementPaymentValidator 混付
    /// （灰优先→纯色补足，黑白参与通用支付）。黑白元素获取通道唯一=卡结算（错边/Payload 补偿）。
    /// </summary>
    public static class ElementCostPayment
    {
        /// <summary>非破坏性预检：当前 bank 是否可支付全部元素代价。</summary>
        public static bool CanPay(List<CostInstance> elementCosts, CostContext ctx)
        {
            var need = AggregateNeed(elementCosts);
            if (need.Count == 0) return true;
            if (ctx?.Payer == null || ctx.ElementPool == null) return false;
            return CanPayNeed(need, ctx.ElementPool.GetPool(ctx.Payer).AvailableMana, ctx);
        }

        /// <summary>支付一组元素代价（原子：先确认可付，再扣）。失败返回 false（调用方中止结算）。</summary>
        public static bool Pay(List<CostInstance> elementCosts, CostContext ctx)
        {
            var need = AggregateNeed(elementCosts);
            if (need.Count == 0) return true;
            if (ctx?.Payer == null || ctx.ElementPool == null) return false;

            var avail = ctx.ElementPool.GetPool(ctx.Payer).AvailableMana;
            if (!CanPayNeed(need, avail, ctx)) return false;
            PayNeed(need, avail, ctx);
            return true;
        }

        private static Dictionary<ManaType, int> AggregateNeed(List<CostInstance> elementCosts)
        {
            var need = new Dictionary<ManaType, int>();
            if (elementCosts == null) return need;
            foreach (var c in elementCosts)
            {
                if (c == null || c.Type != CostType.ElementConsume || c.Value <= 0) continue;
                need.TryGetValue(c.ManaType, out var prev);
                need[c.ManaType] = prev + c.Value;
            }
            return need;
        }

        private static bool CanPayNeed(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            // 纯色浓度上限：每种纯色单次支付量 ≤ 当场地牌槽上限（灰不受限）
            int? cap = GetPureColorCap(ctx);
            foreach (var kv in need)
            {
                if (kv.Value <= 0) continue;
                var affinity = kv.Key == ManaType.Gray
                    ? ElementAffinity.Generic
                    : ElementAffinity.Single(kv.Key);
                if (!ElementPaymentValidator.CanPay(affinity, avail, kv.Value, cap))
                    return false;
            }
            return true;
        }

        /// <summary>当前纯色浓度上限（= 支付者地牌槽上限）；上下文不全时退化为不限制。</summary>
        private static int? GetPureColorCap(CostContext ctx)
            => ctx?.ElementPool != null && ctx.Payer != null
                ? ctx.ElementPool.GetLandCap(ctx.Payer)
                : (int?)null;

        private static void PayNeed(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            var paid = new Dictionary<int, float>();
            int? cap = GetPureColorCap(ctx);
            foreach (var kv in need)
            {
                if (kv.Value <= 0) continue;
                var affinity = kv.Key == ManaType.Gray
                    ? ElementAffinity.Generic
                    : ElementAffinity.Single(kv.Key);
                var plan = ElementPaymentValidator.GetPaymentPlan(affinity, avail, kv.Value, cap);
                if (plan == null) continue;
                foreach (var p in plan)
                {
                    avail[p.Key] -= p.Value;
                    paid.TryGetValue((int)p.Key, out var prev);
                    paid[(int)p.Key] = prev + p.Value;
                }
            }

            if (paid.Count > 0)
            {
                EventManager.Instance.Publish(new ElementPoolPayEvent
                {
                    Player = ctx.Payer,
                    PaidCost = paid
                });
            }
        }
    }

    // ================================================================
    // 代价补偿服务（2026-09-11 黑白元素经济定案）
    // ================================================================

    /// <summary>卡级代价的选择（2026-09-11 定案：代价可选——使用时多一个选择窗口）。</summary>
    public enum OptionalCostChoice
    {
        /// <summary>不付代价：原价支付元素费，代价不执行（无补偿）。</summary>
        Skip = 0,
        /// <summary>支付代价 + 减费：元素账单按当量削减（可减至 0）。</summary>
        Discount = 1,
        /// <summary>支付代价 + 得黑白：按当量获得黑/白元素（封顶地牌上限）。</summary>
        Elements = 2,
    }

    /// <summary>
    /// 代价补偿服务（2026-09-11 黑白元素经济定案；2026-09-14 代价原子化后仅剩 Payload 一种代价）。
    /// **卡级代价可选**（使用时的选择窗口，除抉择窗口外）：
    /// 不付（原价）/ 付+减费（元素账单−当量）/ 付+得黑白（黑=己方侧、白=对方侧，封顶地牌上限）——
    /// 避免「产生了用不了，白白承受代价」。
    /// 当量=Payload 原子**全价**（PayloadUnitGrant：按 Once/单目标/战场落区合成组合层计价，
    /// 原子表为唯一锚——弃牌/送墓/流失等资源支付一律走原子，不再有第二套当量表）。
    /// 启动式/动态效果的代价仍为强制支付+得黑白（发动条件，见 PayWithCompensationAsync）。
    /// </summary>
    public static class CostCompensationService
    {
        /// <summary>单条代价的当量（元素数）：Payload 按原子全价；元素消耗不当量（非「额外代价」）。</summary>
        public static int EquivalentValue(CostInstance cost)
        {
            if (cost == null) return 0;
            if (cost.Type == CostType.Payload)
                return CostDerivationService.PayloadUnitGrant(cost.Payload);
            return 0; // 元素消耗本身不是「额外代价」，不当量
        }

        /// <summary>一组代价的当量合计。</summary>
        public static int TotalEquivalent(List<CostInstance> costs)
        {
            int total = 0;
            if (costs == null) return 0;
            foreach (var c in costs)
                if (c != null) total += EquivalentValue(c);
            return total;
        }

        /// <summary>补偿上限（生成黑白与代价抵扣**共用**，2026-09-11 定案）：当前地牌槽上限——
        /// 防第一回合重代价直接换出超大生物/巨量黑白。黑/白生成与减费两通道同源封顶。</summary>
        public static int CompensationCap(CostContext ctx)
            => ctx?.ElementPool != null && ctx.Payer != null ? Math.Max(0, ctx.ElementPool.GetLandCap(ctx.Payer)) : 0;

        /// <summary>封顶后的当量：min(合计当量, 地牌上限)——减费通道用（黑白生成按条在 IssueGrant 封顶）。</summary>
        public static int CappedTotalEquivalent(List<CostInstance> costs, CostContext ctx)
            => Math.Min(TotalEquivalent(costs), CompensationCap(ctx));

        /// <summary>
        /// 元素账单按当量减费（从最高需求色减起，可减至 0——沿用旧抵消的贪心口径；原地修改）。
        /// </summary>
        public static void ApplyDiscount(Dictionary<int, float> elementBill, int amount)
        {
            if (elementBill == null) return;
            while (amount > 0)
            {
                int bestKey = -1;
                float bestVal = 0;
                foreach (var kv in elementBill)
                {
                    if (kv.Value > bestVal) { bestVal = kv.Value; bestKey = kv.Key; }
                }
                if (bestKey < 0) break;
                int take = Math.Min(amount, (int)elementBill[bestKey]);
                elementBill[bestKey] -= take;
                if (elementBill[bestKey] <= 0) elementBill.Remove(bestKey);
                amount -= take;
            }
        }

        /// <summary>
        /// 卡级代价的可选支付流程（cast 付费步——抉择窗口之后的选择窗口）：
        /// 交互（有 UI 非 AI）弹 1-of-3；AI 防御性策略（原价付不起且减费后付得起 → 付+减费，
        /// 不主动承受牺牲）；无头/超时默认不付。forcedChoice 供验证器直测三路径。
        /// 返回 false=不可中止的异常态（Payer 缺失）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> PayOptionalCardCostsAsync(
            List<CostInstance> costs, CostContext ctx, Dictionary<int, float> elementBill,
            OptionalCostChoice? forcedChoice = null)
        {
            if (costs == null || costs.Count == 0) return true;
            if (ctx?.Payer == null) return false;

            var choice = OptionalCostChoice.Skip;

            if (forcedChoice.HasValue)
            {
                choice = forcedChoice.Value;
            }
            else
            {
                // 代价均为 Payload（强制效果，恒可付）——窗口恒可用（2026-09-14 代价原子化后无预检面）
                {
                    bool interactive = TargetSelectionService.Current != null && !ctx.Payer.IsAI;
                    if (interactive)
                    {
                        int eq = CappedTotalEquivalent(costs, ctx);
                        var labels = new List<string>
                        {
                            "不付代价（原价支付）",
                            eq > 0 ? $"支付代价，费用 −{eq}" : "支付代价，减费",
                            "支付代价，获得黑/白元素",
                        };
                        int idx = await TargetSelectionService.RequestOneIndexAsync(ctx.Payer, labels, "代价选择");
                        choice = idx == 1 ? OptionalCostChoice.Discount
                              : idx == 2 ? OptionalCostChoice.Elements
                              : OptionalCostChoice.Skip;
                    }
                    else if (ctx.Payer.IsAI && ctx.ElementPool != null && elementBill != null)
                    {
                        // AI 策略（2026-09-13 用户裁决）：看手里有没有黑白费卡——
                        // 有（后续用得上黑白资源）→ 付+得黑白；没有 → 付+减费。
                        int eq = CappedTotalEquivalent(costs, ctx);
                        if (eq > 0)
                        {
                            bool needsBW = ctx.ZoneManager != null
                                && (ctx.ZoneManager.GetCards(ctx.Payer, Zone.Hand) ?? new List<Card>())
                                    .Exists(c => (c as CardWrapper)?.GetData()?.Cost?.Keys
                                        .Any(k => k == (int)ManaType.Black || k == (int)ManaType.White) == true);
                            choice = needsBW ? OptionalCostChoice.Elements : OptionalCostChoice.Discount;
                        }
                    }
                }
            }

            if (choice == OptionalCostChoice.Skip) return true;

            // 2026-09-13 修复：Elements 补偿先于代价执行——Payload 执行会经 SummonTokenHandler 的
            // new CardWrapper(template) 触发 EnsureCost 给无费模板补建议价（30/30 模板 0→22），
            // "执行后再算当量"会让高价值复制的补偿意外按膨胀身价计（封顶失真）。补偿按打出时点全价。
            if (choice == OptionalCostChoice.Elements)
                foreach (var cost in costs)
                    if (cost != null) IssueGrant(cost, ctx);

            // 执行代价（全部为 Payload 原子：付费步异步执行）
            foreach (var cost in costs)
            {
                if (cost == null) continue;
                await ExecutePayloadAsync(cost, ctx);
            }

            if (choice == OptionalCostChoice.Discount)
                ApplyDiscount(elementBill, CappedTotalEquivalent(costs, ctx)); // 减费与黑白共用上限

            return true;
        }

        /// <summary>
        /// 强制支付一组代价并逐条发放补偿（启动式/动态效果路径——代价=发动条件，无选择窗口）。
        /// Payload 在此异步执行（恒可付=强制效果）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> PayWithCompensationAsync(
            List<CostInstance> costs, CostContext ctx)
        {
            if (costs == null || costs.Count == 0) return true;
            if (ctx?.Payer == null) return false;

            foreach (var cost in costs)
            {
                if (cost == null) continue;
                // 2026-09-13 修复：补偿先于执行——Payload 执行经 CardWrapper 构造触发
                // EnsureCost 补建议价，执行后算当量会按膨胀身价计（同 PayOptionalCardCostsAsync）
                IssueGrant(cost, ctx);
                await ExecutePayloadAsync(cost, ctx);
            }
            return true;
        }

        /// <summary>单条代价的补偿发放（每条=一次发放事件，封顶地牌上限）。Payload 按原子全价+极性色。</summary>
        private static void IssueGrant(CostInstance cost, CostContext ctx)
        {
            if (ctx?.Payer == null || ctx.ElementPool == null) return;

            int amount = CostDerivationService.PayloadUnitGrant(cost.Payload);
            ManaType color = PayloadGrantColor(cost.Payload);
            if (amount <= 0) return;

            int cap = ctx.ElementPool.GetLandCap(ctx.Payer);
            amount = Math.Min(amount, Math.Max(0, cap));
            if (amount <= 0) return;

            ctx.ElementPool.AddMana(ctx.Payer, color, ctx.Source as Card, amount);
        }

        /// <summary>Payload 补偿颜色：极性非零按极性（有害→黑/有益→白）；零极性按域侧（对方锁→白，其余→黑）。</summary>
        public static ManaType PayloadGrantColor(AtomicEffectInstance atom)
        {
            if (atom == null) return ManaType.Black;
            if (atom.Polarity != 0f) return CostDerivationService.PolarityGrantColor(atom.Polarity);
            return CostDerivationService.SideLock(atom.TargetKinds) == 1 ? ManaType.White : ManaType.Black;
        }

        /// <summary>执行 Payload 原子：按其自身域解析目标（如「给对手召唤」→ 对方侧），无域以无目标执行。
        /// 受惠侧执行：对方域锁定的 payload 以**对手**为控制者执行——产出型原子（SummonToken 等）
        /// 落在控制者侧，给对手的代价自然落在对手战场；己方/双侧以支付者执行。</summary>
        private static async Cysharp.Threading.Tasks.UniTask ExecutePayloadAsync(CostInstance cost, CostContext ctx)
        {
            var atom = cost.Payload;
            if (atom == null) return;

            var executor = CostDerivationService.SideLock(atom.TargetKinds) == 1 && ctx.Payer.Opponent != null
                ? ctx.Payer.Opponent
                : ctx.Payer;

            var ectx = new EffectExecutionContext
            {
                Controller = executor,
                Source = ctx.Payer, // 来源=支付者角色（来源归因定案 2026-09-09）
                ZoneManager = ctx.ZoneManager,
                ElementPool = ctx.ElementPool,
                Targets = new List<Entity>(),
                Duration = DurationType.Once,          // 与 PayloadUnitGrant 计价同口径
                SummonDropZone = Zone.Battlefield,
            };

            if (atom.TargetKinds != null && atom.TargetKinds.Count > 0 && ctx.ZoneManager != null)
            {
                // 目标域按**支付者视角**解析（2026-09-13 修复）：atom 的"对方域"以支付者为基准编写——
                // 原以 executor（受惠侧控制者）视角解析，{2} 敌方被反解成支付者自己，产出落错侧。
                // 受惠侧控制者只承担"产出落在受惠侧"（SummonToken 落区按目标侧/控制器），不参与域解析。
                var resolveCtx = new EffectExecutionContext
                {
                    Controller = ctx.Payer,
                    Source = ctx.Payer,
                    ZoneManager = ctx.ZoneManager,
                    ElementPool = ctx.ElementPool,
                };
                var resolved = EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, resolveCtx);
                if (resolved != null && resolved.Count > 0)
                    ectx.Targets = resolved;
            }

            await EffectHandlerRegistry.ExecuteEffectAsync(atom, ectx);
        }
    }

    public static class BuiltinCostHandlers
    {
        public static void RegisterAll()
        {
            CostHandlerRegistry.Register(new ElementConsumeCostHandler());
            CostHandlerRegistry.Register(new PayloadCostHandler());
        }
    }
}

