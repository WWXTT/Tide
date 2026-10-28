using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 代价可支付闸（2026-10-28 P1 定案；P1b 补资敌臂）：可用性 = 净费可付 ∧ 代价可支付——
    /// 代价不可支付 → 整卡/整效果**直接不可用**（声明期闸，不是结算期失败）。
    ///
    /// 谓词按原子 EffectType 分派（自伤臂 p&lt;0：弃牌→手牌数、流失生命→当前生命（不允许代价致死）、
    /// 磨牌→牌库张数、有域原子→目标候选非空）；目标候选复用 EffectHandlerRegistry.ResolveCandidates
    /// （支付者视角，与 GameActions.EvaluatePayloadGate 目标门槛同源）。
    /// 资敌臂（2026-10-28 P1b，p&gt;0 锁对方）：判定 = 域内有合法目标，且该目标**确实会被改变**——
    /// 治疗（Damaged）须对方有受伤单位；防护类（圣盾/复生/不灭/帷幕…）须对方有未持有同名
    /// 指示物/关键词的单位；抽牌/送牌须对方手牌未满；禁止「能付但无效果」白嫖减费。
    /// 结算期目标失效（响应窗口内全灭）的回退仍由 PayloadTargetsAllLost 承担——本闸不替代。
    /// </summary>
    public static class CostPayability
    {
        /// <summary>单原子：该原子此刻能否被支付/执行（payer 视角）。</summary>
        public static bool CanPay(AtomicEffectInstance atom, Player payer, ZoneManager zm, CostContext ctx)
        {
            if (atom == null) return true;
            payer = payer ?? ctx?.Payer;
            zm = zm ?? ctx?.ZoneManager;
            if (payer == null || zm == null) return false;

            // 资敌臂（2026-10-28 P1b）：p>0 锁对方——按「确实会被改变」判（禁白嫖）；
            // p<0 自伤臂与无极性兜底走下方 Type 分派不变。
            if (atom.Polarity > 0f)
                return !TryRejectBenefitEnemy(atom, payer, zm, ctx, out _);

            int value = Math.Max(0, atom.Value);
            switch (atom.Type)
            {
                case AtomicEffectType.DiscardCard: // 锁己方手牌
                    return ZoneCount(zm, payer, Zone.Hand) >= value;

                case AtomicEffectType.LifeLoss: // 流失生命/上限——不允许代价致死（严格 >）
                    return payer.Life > value;

                case AtomicEffectType.MillCard: // 磨己方牌库
                    return ZoneCount(zm, payer, Zone.Deck) >= value;

                case AtomicEffectType.Sleep: // 自我沉睡（域恰 {Self}）恒可付
                    if (IsSelfDomain(atom)) return true;
                    return HasDomain(atom) ? HasCandidates(atom, payer, zm, ctx) : true;

                default: // 有域 → 目标候选非空；无域 → true
                    return HasDomain(atom) ? HasCandidates(atom, payer, zm, ctx) : true;
            }
        }

        /// <summary>一组代价：全部可支付才 true（Payload 逐原子；ElementConsume 走处理器注册表）。</summary>
        public static bool AllPayable(IEnumerable<CostInstance> costs, CostContext ctx)
        {
            if (costs == null) return true;
            foreach (var cost in costs)
            {
                if (cost == null) continue;
                if (cost.Type == CostType.Payload)
                {
                    if (cost.Payloads == null) continue;
                    foreach (var atom in cost.Payloads)
                        if (!CanPay(atom, ctx?.Payer, ctx?.ZoneManager, ctx))
                            return false;
                }
                else if (!CostHandlerRegistry.CanPay(cost, ctx))
                    return false;
            }
            return true;
        }

        /// <summary>给 UI/AI 的拒绝原因串（可支付返回 null）：手牌不足/生命不足/牌库不足/无合法目标
        /// /资敌无有效目标（对方满血·已持有防护·手牌满等）。</summary>
        public static string RejectReason(IEnumerable<CostInstance> costs, CostContext ctx)
        {
            if (costs == null) return null;
            var payer = ctx?.Payer;
            var zm = ctx?.ZoneManager;
            foreach (var cost in costs)
            {
                if (cost == null) continue;
                if (cost.Type == CostType.Payload)
                {
                    if (cost.Payloads == null) continue;
                    foreach (var atom in cost.Payloads)
                    {
                        if (atom == null) continue;
                        int value = Math.Max(0, atom.Value);
                        string reason = atom.Type switch
                        {
                            AtomicEffectType.DiscardCard
                                => ZoneCount(zm, payer, Zone.Hand) >= value ? null : $"手牌不足（需弃 {value} 张，现 {ZoneCount(zm, payer, Zone.Hand)} 张）",
                            AtomicEffectType.LifeLoss
                                => payer != null && payer.Life > value ? null : $"生命不足（代价不允许致死：需生命 > {value}）",
                            AtomicEffectType.MillCard
                                => ZoneCount(zm, payer, Zone.Deck) >= value ? null : $"牌库不足（需磨 {value} 张，现 {ZoneCount(zm, payer, Zone.Deck)} 张）",
                            _ => null,
                        };
                        if (reason != null) return reason;
                        if (atom.Polarity > 0f)
                        {
                            // 资敌臂（P1b）：按「确实会被改变」判（禁白嫖）——先于通用候选检查
                            if (TryRejectBenefitEnemy(atom, payer, zm, ctx, out var enemyReason))
                                return enemyReason;
                        }
                        else if (atom.Type != AtomicEffectType.Sleep || !IsSelfDomain(atom)) // 自我沉睡恒可付
                        {
                            // 有域原子：候选空 → 无合法目标
                            if (HasDomain(atom) && !HasCandidates(atom, payer, zm, ctx))
                                return $"代价无合法目标（{atom.Type}）——卡的发动无效";
                        }
                    }
                }
                else if (!CostHandlerRegistry.CanPay(cost, ctx))
                    return $"代价不可支付（{cost.Type}）";
            }
            return null;
        }

        // ======================================== 资敌臂（2026-10-28 P1b：p>0 锁对方） ========================================

        /// <summary>防护类指示物原子 → 同名指示物（执行侧 IndicatorHandler 挂层同源；
        /// 关键词族防护不在此表——走 GrantKeywordHandlerFactory 同名关键词）。</summary>
        private static readonly Dictionary<AtomicEffectType, string> ProtectionCounters =
            new Dictionary<AtomicEffectType, string>
            {
                { AtomicEffectType.GrantDivineShield, Attribute.CounterRules.DivineShieldCounter },
                { AtomicEffectType.GrantReborn, Attribute.CounterRules.RebornCounter },
                { AtomicEffectType.GrantStealth, Attribute.CounterRules.StealthCounter },
                { AtomicEffectType.GrantSpellShield, Attribute.CounterRules.SpellShieldCounter },
                { AtomicEffectType.GrantToughness, Attribute.CounterRules.ToughnessCounter },
            };

        /// <summary>资敌原子可支付判定（P1b §2.3）：域内有合法目标，且该目标**确实会被改变**——
        /// 禁止「能付但无效果」白嫖减费（治疗满血单位、给已持圣盾者再赋圣盾都不算可付）。
        /// 返回 true = 拒绝（reason 非空）；无域原子区域自结算，不作资敌检查（可付）。</summary>
        private static bool TryRejectBenefitEnemy(AtomicEffectInstance atom, Player payer, ZoneManager zm,
            CostContext ctx, out string reason)
        {
            reason = null;
            if (!HasDomain(atom)) return false;

            // 抽牌/送牌族（对方卡域）：对方手牌未满才可付（手牌未满 → 继续候选检查）
            if (atom.Type == AtomicEffectType.DrawCard && HasEnemyCardZone(atom))
            {
                var opp = payer.Opponent;
                if (opp != null && ZoneCount(zm, opp, Zone.Hand) >= RuleHooks.GetHandLimit(opp))
                {
                    reason = $"资敌代价不可付：对方手牌已满（{atom.Type}）——卡的发动无效";
                    return true;
                }
            }

            var candidates = ResolveDomainCandidates(atom, payer, zm, ctx);
            if (candidates.Count == 0)
            {
                reason = $"资敌代价无合法目标（{atom.Type}）——卡的发动无效";
                return true;
            }

            // 治疗：须存在会被改变的受伤目标（行 Damaged 滤通常已收窄，此处按 DamagedFilter
            // 同口径兜底——反疗改写目标视为会被改变，治疗将转伤害恒非白嫖）。
            if (atom.Type == AtomicEffectType.Heal && !candidates.Any(IsHealChangeable))
            {
                reason = $"资敌代价无目标：对方无受伤单位（{atom.Type} 治疗满血=白嫖）——卡的发动无效";
                return true;
            }

            // 防护族（Grant*）：须存在未持有同名指示物/关键词的可赋予目标
            if (atom.Type.ToString().StartsWith("Grant") && TryAlreadyHeld(atom.Type, out var alreadyHeld)
                && !candidates.Any(c => !alreadyHeld(c)))
            {
                reason = $"资敌代价无目标：对方单位均已持有该防护（{atom.Type}）——卡的发动无效";
                return true;
            }

            // 其余（属性增益 +1/+1 等——可增益目标恒会被改变）：候选非空即可付
            return false;
        }

        /// <summary>域内是否含对方侧卡区（送牌族判定输入）。</summary>
        private static bool HasEnemyCardZone(AtomicEffectInstance atom)
            => atom.TargetKinds.Any(k => TargetKindRules.IsCardKind(k) && TargetKindRules.IsEnemySide(k));

        /// <summary>治疗会被改变（与 DamagedFilter 同口径：未满血，或反疗改写——治疗转伤害恒改变目标）。</summary>
        private static bool IsHealChangeable(Entity e)
            => e.GetLife() < e.GetMaxLife() || RuleAuraComponents.IsHealInverted(e);

        /// <summary>防护类原子的「已持有」判定：指示物族=同名指示物层数&gt;0；
        /// 关键词族=同名关键词（GrantKeywordHandlerFactory 唯一映射源）。</summary>
        private static bool TryAlreadyHeld(AtomicEffectType type, out Func<Entity, bool> alreadyHeld)
        {
            if (ProtectionCounters.TryGetValue(type, out var counter))
            {
                alreadyHeld = e => e.GetCounterCount(counter) > 0;
                return true;
            }
            if (Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(type, out var keyword))
            {
                alreadyHeld = e => e.HasKeyword(keyword);
                return true;
            }
            alreadyHeld = null;
            return false;
        }

        // ======================================== 内部 ========================================

        private static bool HasDomain(AtomicEffectInstance atom)
            => atom.TargetKinds != null && atom.TargetKinds.Count > 0;

        private static bool IsSelfDomain(AtomicEffectInstance atom)
            => atom.TargetKinds != null && atom.TargetKinds.Count == 1
               && atom.TargetKinds[0] == (int)TargetKind.Self;

        private static int ZoneCount(ZoneManager zm, Player player, Zone zone)
        {
            if (zm == null || player == null) return 0;
            var cards = zm.GetCards(player, zone);
            return cards?.Count ?? 0;
        }

        /// <summary>域候选解析（支付者视角——与 EvaluatePayloadGate 目标门槛同源谓词）。</summary>
        private static List<Entity> ResolveDomainCandidates(AtomicEffectInstance atom, Player payer,
            ZoneManager zm, CostContext ctx)
        {
            if (payer == null || zm == null) return new List<Entity>();
            var resolveCtx = new EffectExecutionContext
            {
                Controller = payer,
                Source = payer,
                ZoneManager = zm,
                ElementPool = ctx?.ElementPool,
            };
            return Attribute.EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, resolveCtx)
                   ?? new List<Entity>();
        }

        private static bool HasCandidates(AtomicEffectInstance atom, Player payer, ZoneManager zm, CostContext ctx)
            => ResolveDomainCandidates(atom, payer, zm, ctx).Count > 0;
    }
}
