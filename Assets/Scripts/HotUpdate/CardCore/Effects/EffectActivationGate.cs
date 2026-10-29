using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 主动效果发动闸（2026-10-29 P5：主动效果可带一条代价行——构筑两行「效果行 + 可选代价行」）。
    /// · 净费 = max(0, 发动费 − Σ代价价)，抵扣逐点先灰、后最高费用色（复用
    ///   <see cref="CardCompositionCost.ReduceBuckets"/>，与卡层代价抵扣同算法）；
    /// · 每次发动各扣一次净费、各执行一次代价原子（代价只执行、不产任何元素——P1 口径）；
    /// · 顺序不回退：净费先付、代价后执行（与卡层 cast 同序）；
    /// · 可用性 = 净费可付 ∧ 代价可支付（<see cref="CostPayability"/>——资敌白嫖/手牌不足在此拦，
    ///   不可付 → 效果不可发动：引擎 CanActivate 拒 + UI 灰显）。
    /// 不开放的口：技能栏（HeroSkillSystem.ActivationBill 不挂代价、不套抵扣——调用方按
    /// IsSkillSource 分流回既有路径）；触发式（ElementCostPrepaid，不经引擎现付段）。
    /// 消费口：EffectExecutor.CanActivate 预检 / ExecuteAsync 现付段 / 两个合成器净费显示 / 验证器。
    /// </summary>
    public static class EffectActivationGate
    {
        /// <summary>是否走本闸：主动效果 + 携带效果级代价 + 非技能源。
        /// （引擎预检与现付段共用；false = 调用方走既有 CostHandlerRegistry/全额路径。）</summary>
        public static bool Applies(EffectDefinition effect, Entity source, List<CostInstance> specialCosts)
            => effect != null && effect.IsActivatedEffect
               && specialCosts != null && specialCosts.Count > 0
               && !HeroSkillSystem.IsSkillSource(source);

        /// <summary>代价抵扣点数：特殊代价里全部 Payload 原子的全价合计
        ///（<see cref="CostDerivationService.PayloadUnitGrant"/> 单源——与构筑期计价同锚）。</summary>
        public static int DeductionPoints(List<CostInstance> specialCosts)
        {
            int points = 0;
            if (specialCosts == null) return 0;
            foreach (var c in specialCosts)
            {
                if (c?.Payloads == null) continue;
                foreach (var atom in c.Payloads)
                    points += CostDerivationService.PayloadUnitGrant(atom);
            }
            return points;
        }

        /// <summary>每次发动的净费账单（发动费 − 代价抵扣；先灰后最高费用色、逐色 ≥0、可归零）。
        /// 只读不改入参；无代价/零抵扣 → 原费克隆。</summary>
        public static ElementCost NetBill(ElementCost elementCosts, List<CostInstance> specialCosts)
        {
            var net = elementCosts?.Clone() ?? new ElementCost();
            int points = DeductionPoints(specialCosts);
            if (points <= 0) return net;
            var buckets = ElementPaymentValidator.NormalizeBill(net);
            CardCompositionCost.ReduceBuckets(buckets, points);
            var result = new ElementCost();
            foreach (var kv in buckets)
                result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>可用性（引擎预检/UI 灰显共用）：净费可付 ∧ 代价可支付。
        /// 可用返回 null；不可用返回拒绝原因（净费段/代价段各口径）。
        /// 前置：Applies 为真（非本闸管辖的组合返回 null，调用方走既有路径）。</summary>
        public static string RejectReason(EffectDefinition effect, Entity source,
            ElementCost elementCosts, List<CostInstance> specialCosts, CostContext ctx)
        {
            if (!Applies(effect, source, specialCosts)) return null;
            var net = NetBill(elementCosts, specialCosts);
            if (!net.IsZero && !ElementCostPayment.CanPay(net, ctx))
                return $"净费不可付（发动费 {elementCosts} − 代价抵扣后余额不足）";
            return CostPayability.RejectReason(specialCosts, ctx);
        }
    }
}
