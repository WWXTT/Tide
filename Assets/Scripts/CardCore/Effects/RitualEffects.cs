using System.Collections.Generic;
using CardCore.Attribute;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 仪式光环效果（完成后由 RitualSystem 常驻驱动，完成者独享）。
    /// 光环封顶约束：等价于"占 1 个非生物区格 + 修改一条规则"，不得产生资源类每回合净增长
    /// （防资源轴复利叠加失控）——两张样板均合规：①守恒兑换（3 同色→3 异色，无净增），②改支付去向。
    /// </summary>
    public static class RitualEffects
    {
        /// <summary>
        /// 三相仪典：完成者每消耗 3 点相同纯色元素 → 获得红/蓝/绿各 1。
        /// 只计完成者自己的消耗（ElementPoolPayEvent 两处发布——出牌支付与效果元素费——都算）；
        /// 余数保留继续累计。守恒兑换：3 换 3，无净增长。
        /// </summary>
        public static void OnElementPaid(ElementPoolPayEvent e)
        {
            var core = GameCore.Instance;
            if (core == null || e?.Player == null || e.PaidCost == null) return;

            foreach (var aura in RitualSystem.CompletedAuras)
            {
                if (aura?.Definition?.aura?.effectId != "ElementConversion") continue;
                if (e.Player != aura.Completer) continue; // 完成者独享

                foreach (var kv in e.PaidCost)
                {
                    var color = (ManaType)kv.Key;
                    if (color != ManaType.Red && color != ManaType.Blue && color != ManaType.Green) continue;

                    aura.SpendCounters.TryGetValue(color, out var count);
                    count += (int)kv.Value;
                    while (count >= 3)
                    {
                        count -= 3;
                        GrantTrinity(core, aura.Completer, aura.Card);
                    }
                    aura.SpendCounters[color] = count;
                }
            }
        }

        /// <summary>红/蓝/绿各 +1 入 bank（照 AddManaHandler 先例：直写 AvailableMana + 发 AddManaEvent）。</summary>
        private static void GrantTrinity(GameCore core, Player completer, Entity source)
        {
            var bank = core.ElementPool.GetPool(completer).AvailableMana;
            foreach (var color in new[] { ManaType.Red, ManaType.Blue, ManaType.Green })
            {
                bank.TryGetValue(color, out var cur);
                bank[color] = cur + 1;
                EventManager.Instance.Publish(new AddManaEvent
                {
                    Player = completer,
                    ManaType = color,
                    Amount = 1,
                    Source = source
                });
            }
        }

        // ======================================== 血偿仪典：生命代价改由对手支付 ========================================

        private static bool _bloodPactRegistered;

        /// <summary>
        /// 覆盖注册生命代价装饰器（在 BuiltinCostHandlers.RegisterAll 之后调用一次，见 EffectExecutionEngine 注册链尾部）。
        /// 装饰器内部查询 RitualSystem 状态：无血偿完成者时行为与原处理器完全一致，Reset 不撤销。
        /// </summary>
        public static void RegisterBloodPact()
        {
            if (_bloodPactRegistered) return;
            _bloodPactRegistered = true;
            var original = CostHandlerRegistry.GetHandler(CostType.LifePayment);
            CostHandlerRegistry.Register(new BloodPactLifePaymentHandler(original));
        }

        /// <summary>payer 是否为某已完成血偿仪典的完成者（是 → 其生命代价转由对手承担）。</summary>
        public static bool IsBloodPactCompleter(Player payer)
        {
            if (payer == null) return false;
            foreach (var aura in RitualSystem.CompletedAuras)
            {
                if (aura?.Definition?.aura?.effectId == "BloodPact" && aura.Completer == payer)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 血偿装饰器：完成者支付生命代价时 Payer 换为对手（CanPay 同样看对手——严格大于，不能付到 0）。
        /// LifePaymentCostEvent.Player = 实际失去生命的一方（对手）。
        /// </summary>
        private class BloodPactLifePaymentHandler : ICostHandler
        {
            private readonly ICostHandler _fallback;

            public BloodPactLifePaymentHandler(ICostHandler fallback) => _fallback = fallback;

            public CostType CostType => CostType.LifePayment;

            public bool CanPay(CostInstance cost, CostContext context)
            {
                if (IsBloodPactCompleter(context?.Payer))
                {
                    var payer = context.Payer.Opponent;
                    return payer != null && payer.Life > cost.Value;
                }
                return _fallback != null && _fallback.CanPay(cost, context);
            }

            public void Pay(CostInstance cost, CostContext context)
            {
                if (IsBloodPactCompleter(context?.Payer))
                {
                    var payer = context.Payer.Opponent; // 实际支付者
                    payer.Life -= cost.Value;
                    EventManager.Instance.Publish(new LifePaymentCostEvent
                    {
                        Player = payer,
                        Amount = cost.Value,
                        Source = context.Source
                    });
                    return;
                }
                _fallback?.Pay(cost, context);
            }

            public string GetDescription(CostInstance cost) => $"支付 {cost.Value} 点生命";
        }
    }
}
