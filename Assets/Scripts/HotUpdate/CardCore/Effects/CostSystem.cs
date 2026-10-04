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

        /// <summary>声明期预选的代价目标（出牌两阶段 2026-10-04 定案）：
        /// 出牌声明期（PlayCard/PlayCardInResponse）预选、cast 结算付费步消费——
        /// ExecutePayloadAsync 优先沿用，不在结算期重选（响应窗口内状态变化不重选，
        /// 死亡/离场目标由各 handler 自行过滤）。null = 非出牌路径（启动式/动态效果），结算期照常解析。</summary>
        public List<Entity> PreselectedCostTargets;
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
            var costArr = ElementCost.FromValue(cost.ManaType, cost.Value);
            // 自动横置补足（2026-09-30）：与 GameActions.CanAfford / 出牌付费同口径
            return context.ElementPool.CanPayCostWithAutoTap(costArr, context.Payer);
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var costArr = ElementCost.FromValue(cost.ManaType, cost.Value);
            // 战报来源标注（2026-09-21）：效果费挂在哪个来源（场上卡/角色）上
            string note = context?.Source != null ? "效果费·" + EffectText.Name(context.Source) : "效果费";
            context.ElementPool.TryPayCostWithAutoTap(costArr, context.Payer, context.ZoneManager, note);
        }

        public string GetDescription(CostInstance cost)
        {
            return $"消耗 {cost.Value} 点{cost.ManaType} 元素";
        }
    }

    // ================================================================
    // 代价相关事件（域事件——发布方为原子 handler，消费方=对局统计）
    // ================================================================
    // 2026-09-14 代价原子化：资源类代价处理器（弃牌/生命/沉睡/送墓/自紊乱/对手增益）随
    // CostOffset 抵消系统退役——资源支付改由**代价栏 Payload 原子**承载（付费步执行 +
    // 错边全价补偿黑/白）。下列域事件保留：改由对应原子 handler 发布——
    // MillCard（磨**自己**牌库=送墓语义）与 LifeLoss（流失**自己**生命上限=支付语义）
    // 在归属==控制者时发布，MatchStatsService 等既有订阅零改动。

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
    /// 元素代价支付（纯支付，无兑换）：按颜色聚合需求 → 账单规划器一次出方案 → 扣款 + 支付事件。
    /// 2026-09-14 统一混付：与出牌（ElementPool.PayCost）共用 ElementPaymentValidator.GetBillPaymentPlan——
    /// 支付序=同色→灰→黑→白（黑白=万用色单向替代四色）；每种货币（**含灰**）单次贡献 ≤ 地牌槽上限；
    /// 纯色需求量超上限不论货币不可付。黑白获取通道唯一=卡结算（错边/Payload 补偿；2026-10-04 起产出不封，约束=支付单次贡献≤上限）。
    /// </summary>
    public static class ElementCostPayment
    {
        /// <summary>非破坏性预检：当前 bank 是否可支付全部元素代价。
        /// 自动横置补足（2026-09-30）：bank 不足但地牌可产所需元素亦视为可付（与出牌门禁同口径）。</summary>
        public static bool CanPay(List<CostInstance> elementCosts, CostContext ctx)
        {
            var need = AggregateNeed(elementCosts);
            if (need.Count == 0) return true;
            if (ctx?.Payer == null || ctx.ElementPool == null) return false;
            if (ElementPaymentValidator.CanPayBill(
                    need, ctx.ElementPool.GetPool(ctx.Payer).AvailableMana, GetPureColorCap(ctx)))
                return true;
            return ctx.ElementPool.CanPayCostWithAutoTap(need, ctx.Payer);
        }

        /// <summary>同 CanPay（2026-10-04 位置数组口径——DeriveElementCosts 输出直付）。</summary>
        public static bool CanPay(ElementCost elementCosts, CostContext ctx)
        {
            var need = ElementPaymentValidator.NormalizeBill(elementCosts);
            if (need.Count == 0) return true;
            if (ctx?.Payer == null || ctx.ElementPool == null) return false;
            if (ElementPaymentValidator.CanPayBill(
                    need, ctx.ElementPool.GetPool(ctx.Payer).AvailableMana, GetPureColorCap(ctx)))
                return true;
            return ctx.ElementPool.CanPayCostWithAutoTap(need, ctx.Payer);
        }

        /// <summary>支付一组元素代价（原子：整账单一次规划，失败不动 bank）。失败返回 false（调用方中止结算）。
        /// 支付事件携带实际货币组合（如红1 账单付 {黑1}）。
        /// bank 不足时先自动横置地牌补足（2026-09-30 定案——声明期 CanPay 同口径放行，此处实际横置）。</summary>
        public static bool Pay(List<CostInstance> elementCosts, CostContext ctx)
        {
            var need = AggregateNeed(elementCosts);
            return PayBill(need, ctx);
        }

        /// <summary>同 Pay（2026-10-04 位置数组口径——DeriveElementCosts 输出直付）。</summary>
        public static bool Pay(ElementCost elementCosts, CostContext ctx)
        {
            var need = ElementPaymentValidator.NormalizeBill(elementCosts);
            return PayBill(need, ctx);
        }

        private static bool PayBill(Dictionary<ManaType, int> need, CostContext ctx)
        {
            if (need.Count == 0) return true;
            if (ctx?.Payer == null || ctx.ElementPool == null) return false;

            var avail = ctx.ElementPool.GetPool(ctx.Payer).AvailableMana;
            var plan = ElementPaymentValidator.GetBillPaymentPlan(need, avail, GetPureColorCap(ctx));
            if (plan == null)
            {
                if (!ctx.ElementPool.AutoTapForBill(need, ctx.Payer, ctx.ZoneManager)) return false;
                plan = ElementPaymentValidator.GetBillPaymentPlan(need, avail, GetPureColorCap(ctx));
                if (plan == null) return false;
            }

            foreach (var kv in plan)
                avail[kv.Key] -= kv.Value;

            if (plan.Count > 0)
            {
                EventManager.Instance.Publish(new ElementPoolPayEvent
                {
                    Player = ctx.Payer,
                    PaidCost = plan.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value)
                });
            }
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

        /// <summary>当前浓度上限（= 支付者地牌槽上限；灰与万用黑白同受此约束）；上下文不全时退化为不限制。</summary>
        private static int? GetPureColorCap(CostContext ctx)
            => ctx?.ElementPool != null && ctx.Payer != null
                ? ctx.ElementPool.GetLandCap(ctx.Payer)
                : (int?)null;
    }

    // ================================================================
    // 代价补偿服务（2026-09-11 黑白元素经济定案；2026-09-14 代价强制化）
    // ================================================================

    /// <summary>
    /// 代价补偿服务（2026-09-14 定案：**代价强制**——撤销 09-11 的「代价可选」三选一窗口；
    /// 2026-10-04 定案：**任意单向不限价**——「等价1」限价退役，任意单向效果可作 Payload
    ///（放置口经 CostDerivationService.PayloadCostDomain 逆转选择范围），补偿按全价发放）。
    /// cast 付费步与启动式/动态效果统一走 PayWithCompensationAsync：
    /// Payload 原子**强制执行** + 按全价获得黑（己方侧）/白（对方侧）——无选择窗口、无减费通道。
    /// 补偿数量=Payload 原子全价（PayloadUnitGrant：按 Once/单目标/战场落区合成组合层计价，
    /// 原子表为唯一锚）。2026-10-04：产出不封·全量入账（旧 AddMana 每回合钳制退役——
    /// 约束移到使用侧支付浓度上限）；本轮追加：**补偿后置**（执行生效后按快照全价立即获得）、
    /// 代价无有效目标→不执行不补偿、全价在声明期过地牌门槛（GameActions.EvaluatePayloadGate）。
    /// </summary>
    public static class CostCompensationService
    {
        /// <summary>
        /// 强制支付一组代价并逐条发放补偿（cast 付费步与启动式/动态效果共用的唯一路径）。
        /// Payload 在此异步执行（恒可付=强制效果）。
        /// 顺序（2026-10-04 本轮定案）：**补偿后置**——先快照全价（Payload 执行会膨胀模板身价，
        /// 补偿按打出时点全价——09-13 护栏维持），执行**生效后**立即以快照值发放黑/白；
        /// 代价没有作用于有效目标（无可用目标）→ 不执行、不发放（走不到代价黑白元素生成——
        /// 整卡出牌路径的回退拦截在 GameActions.ResolveCardCastAsync 扣费前）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> PayWithCompensationAsync(
            List<CostInstance> costs, CostContext ctx)
        {
            if (costs == null || costs.Count == 0) return true;
            if (ctx?.Payer == null) return false;

            foreach (var cost in costs)
            {
                if (cost == null) continue;
                int grantAmount = CostDerivationService.PayloadUnitGrant(cost.Payload); // 快照：执行前全价
                if (await ExecutePayloadAsync(cost, ctx))
                    IssueGrant(cost, ctx, grantAmount); // 生效后立即获得黑白（快照值）
            }
            return true;
        }

        /// <summary>单条代价的补偿发放（每条=一次发放事件）。Payload 按原子全价+极性色；
        /// 2026-10-04 本轮：补偿后置——发放量=执行前快照（见 PayWithCompensationAsync），生效后立即入账；
        /// 产出不封定案维持：全量入账（约束在使用侧支付浓度上限）。</summary>
        private static void IssueGrant(CostInstance cost, CostContext ctx, int amount)
        {
            if (ctx?.Payer == null || ctx.ElementPool == null || amount <= 0) return;

            ManaType color = PayloadGrantColor(cost.Payload);
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
        /// 落在控制者侧，给对手的代价自然落在对手战场；己方/双侧以支付者执行。
        /// 返回是否**作用于有效目标**（2026-10-04 本轮定案）：有域而目标为空 → false（不执行、不补偿——
        /// 走不到代价黑白元素生成）；无域（无目标语义）/正常执行 → true。</summary>
        private static async Cysharp.Threading.Tasks.UniTask<bool> ExecutePayloadAsync(CostInstance cost, CostContext ctx)
        {
            var atom = cost.Payload;
            if (atom == null) return true;

            bool hasDomain = atom.TargetKinds != null && atom.TargetKinds.Count > 0;

            var executor = hasDomain && CostDerivationService.SideLock(atom.TargetKinds) == 1 && ctx.Payer.Opponent != null
                ? ctx.Payer.Opponent
                : ctx.Payer;

            var ectx = new EffectExecutionContext
            {
                Controller = executor,
                Source = ctx.Payer, // 来源=支付者角色（来源归因定案 2026-09-09）
                ZoneManager = ctx.ZoneManager,
                ElementPool = ctx.ElementPool,
                Duration = DurationType.Once,          // 与 PayloadUnitGrant 计价同口径
                SummonDropZone = Zone.Battlefield,
            };

            List<Entity> targets;
            if (!hasDomain)
            {
                targets = new List<Entity>(); // 无域代价（无目标语义）：按无目标执行、照常补偿
            }
            else if (ctx.PreselectedCostTargets != null)
            {
                // 出牌两阶段（2026-10-04 定案）：声明期预选的代价目标优先沿用（不重选——
                // 响应窗口内目标死亡/离场由各 handler 过滤；全灭拦截在 ResolveCardCastAsync 扣费前）。
                targets = new List<Entity>(ctx.PreselectedCostTargets);
            }
            else
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
                targets = EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, resolveCtx)
                    ?? new List<Entity>();
            }

            if (hasDomain && targets.Count == 0)
            {
                // 2026-10-04 本轮定案：代价没有作用于有效目标 → 不执行、不补偿
                return false;
            }

            ectx.Targets = targets;
            await EffectHandlerRegistry.ExecuteEffectAsync(atom, ectx);
            return true;
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

