using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 主动效果发动闸（2026-10-29 P5：主动效果可带一条代价行——构筑两行「效果行 + 可选代价行」）。
    /// · 净费 = max(0, 发动费 − Σ代价价)，**逐行色锁**（2026-11-05 收口定案：每行只在自身原子
    ///   可产出色内逐点扣，先灰后集合内最高费用色——<see cref="CardCostService.ApplyLockedDeduction"/>
    ///   单源，与卡层代价抵扣同口径；跨色不抵、溢出丢弃）；
    /// · 每次发动各扣一次净费、各执行一次代价原子（代价只执行、不产任何元素——P1 口径）；
    /// · 顺序不回退：净费先付、代价后执行（与卡层 cast 同序）；
    /// · 可用性 = 净费可付 ∧ 代价可支付（<see cref="CostPayability"/>——资敌白嫖/手牌不足在此拦，
    ///   不可付 → 效果不可发动：引擎 CanActivate 拒 + UI 灰显）。
    /// 不开放的口：技能栏（技能卡禁带代价——资格闸 CanBeSkillCard 拦，见 HeroSkillSystem；
    /// IsSkillSource 分流保底：技能源效果即便带代价也不套本闸抵扣）；触发式（ElementCostPrepaid，
    /// 不经引擎现付段）。
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
        ///（<see cref="CostDerivationService.PayloadUnitGrant"/> 单源——与构筑期计价同锚）。
        /// **颜色盲读**（潜在抵扣上限）：实际落账以 <see cref="NetBill"/> 逐行色锁为准——
        /// 代价色与账单色无交集时实际抵扣可小于本值（UI 抵扣角标只作潜在值展示）。</summary>
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

        /// <summary>每次发动的净费账单（发动费 − 代价抵扣；**逐行色锁**、逐色 ≥0、可归零）。
        /// 只读不改入参；无代价/零抵扣 → 原费克隆。
        /// 2026-11-05 色锁收口定案（对齐卡层 ApplyCastDeduction/ApplyPayloadDeduction 同口径）：
        /// 抵扣单位=单个代价行（行序=声明序），每行只在自身原子**可产出色**（行 ManaList 非零色）
        /// 内逐点扣（先灰后该集合内最高费用色——<see cref="CardCostService.ApplyLockedDeduction"/> 单源）；
        /// 跨色不抵、溢出丢弃——蓝代价抵不了红/灰账（弹回红色化后蓝套代价跨色案定案）。
        /// 2026-10-30 真实小数定案：抵扣直接在 float 桶上逐点进行（每点至多减 1.0、下限 0、
        /// ≤1e-4 视为空桶），净费保留真值小数——取整只在支付终点 NormalizeBill（逐色进1）。</summary>
        public static ElementCost NetBill(ElementCost elementCosts, List<CostInstance> specialCosts)
        {
            var net = elementCosts?.Clone() ?? new ElementCost();
            if (specialCosts == null) return net;
            foreach (var c in specialCosts)
            {
                if (c == null || c.Type != CostType.Payload || c.Payloads == null) continue;
                int points = 0;
                var allowed = new HashSet<ManaType>();
                foreach (var atom in c.Payloads)
                {
                    if (atom == null) continue;
                    points += CostDerivationService.PayloadUnitGrant(atom);
                    var ml = Attribute.AtomicEffectTable.GetByHashId(atom.RowHashId)?.ManaList;
                    if (ml == null) continue;
                    foreach (var color in ml.NonzeroColors())
                        if (color != ManaType.Any)
                            allowed.Add(color);
                }
                CardCostService.ApplyLockedDeduction(net, points, allowed);
            }
            return net;
        }

        /// <summary>可用性（引擎预检/UI 灰显共用）：净费可付 ∧ 代价可支付 ∧ 代价地牌门槛。
        /// 可用返回 null；不可用返回拒绝原因（净费段/代价段/地牌段各口径）。
        /// 前置：Applies 为真（非本闸管辖的组合返回 null，调用方走既有路径）。
        /// 地牌门槛（2026-10-31 代价指向化：「使用主动效果时同理」）——只算**本效果**挂的
        /// 代价行：按色生成面 > 施放者该色地牌数（元素池 LockedColor 计数，与施放口
        /// EvaluatePayloadGate ③ 同尺；等于放行）。</summary>
        public static string RejectReason(EffectDefinition effect, Entity source,
            ElementCost elementCosts, List<CostInstance> specialCosts, CostContext ctx)
        {
            if (!Applies(effect, source, specialCosts)) return null;
            var net = NetBill(elementCosts, specialCosts);
            if (!net.IsZero && !ElementCostPayment.CanPay(net, ctx))
                return $"净费不可付（发动费 {elementCosts} − 代价抵扣后余额不足）";
            var payReject = CostPayability.RejectReason(specialCosts, ctx);
            if (payReject != null) return payReject;

            // ③ 地牌门槛（发动口分账）：本效果代价行按色对齐同色地牌数
            if (ctx?.Payer != null && ctx.ElementPool != null)
            {
                foreach (var kv in CardCostService.PayloadGrantByColor(specialCosts))
                {
                    int lands = ctx.ElementPool.GetPool(ctx.Payer)?.PooledCards?
                        .Count(pc => pc != null && pc.LockedColor == kv.Key) ?? 0;
                    if (kv.Value > lands)
                        return $"{ManaTypeNames.ZhNameOf(kv.Key)}色代价 {kv.Value} 大于当前"
                             + $"{ManaTypeNames.ZhNameOf(kv.Key)}地 {lands}——效果不可发动";
                }
            }
            return null;
        }
    }
}
