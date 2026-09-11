using System;
using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 费用自动推导服务 —— 效果「锚价」（即时价，法术定价：第 1 回合立刻打出来的价格）。
    /// 由效果配置表（AttributeValueConfig.json 的 BaseCost/EffectColor）推导出「元素消耗代价」，
    /// 使费用成为配置表唯一权威：费用 = round(BaseCost × CostMultiplier × 效果值) 个 EffectColor 元素。
    /// 卡牌仍可在 effect.Costs 中显式声明非元素特殊代价（Sleep/SummonMaterial/弃牌 等）。
    /// 生物挂载折扣（落地延迟 d(C) 与存活期望的额外回合折）不在此处 —— 在 CardCostService 的组合层计价。
    /// </summary>
    public static class CostDerivationService
    {
        /// <summary>
        /// 推导一个效果定义的元素消耗代价（不含卡牌显式声明的特殊代价）。
        /// 同色多笔代价会按颜色合并为一笔，便于上层抵消按颜色聚合处理。
        /// modeIndex：抉择（Choice）步骤只计所选模式的原子（per-mode 独立计价定案）；
        /// 无抉择卡恒 0，既有调用点零改动。
        /// priceAsPermanent（三轨制 2026-09-09）：法术宿主置 true——魔法卡赋予全是设置类
        /// （永久直改），**计价按永久效果档估算**；运行时支付路径不传（默认 false，按实例持续）。
        /// </summary>
        public static List<CostInstance> DeriveElementCosts(EffectDefinition effect, int modeIndex = 0)
        {
            var byColor = new Dictionary<ManaType, int>();
            if (effect == null)
                return new List<CostInstance>();

            VisitBillableAtoms(effect, modeIndex,
                (atom, domain) => AccumulateElementCost(atom, effect, domain, byColor));

            var list = new List<CostInstance>();
            foreach (var kv in byColor)
            {
                if (kv.Value <= 0) continue;
                list.Add(new CostInstance
                {
                    Type = CostType.ElementConsume,
                    Value = kv.Value,
                    ManaType = kv.Key
                });
            }
            return list;
        }

        /// <summary>
        /// 推导一个效果定义的黑白元素获得量（2026-09-11 定案：错边原子出计价改发元素）。
        /// 有害原子(p&lt;0)锁己方域 → 黑；有益原子(p&gt;0)锁对方域 → 白；双侧域/无目标构筑期不记
        /// （运行时按实际命中错边目标照发，见 EffectExecutionEngine）。
        /// 数量与原错边折价一致：×|p|（当前表 |p|=1 即全额）；固定数量 ×N 同计费口径。
        /// 动态数量原子构筑期不可知，不记（运行时实判）。
        /// </summary>
        public static Dictionary<ManaType, int> DeriveElementGrants(EffectDefinition effect, int modeIndex = 0)
        {
            var grants = new Dictionary<ManaType, int>();
            if (effect == null)
                return grants;

            VisitBillableAtoms(effect, modeIndex,
                (atom, domain) => AccumulateElementGrant(atom, effect, domain, grants));
            return grants;
        }

        /// <summary>
        /// 计费原子遍历（费用与黑白获得共用同一口径，防漂移）：
        /// 节点化：仅主序列原子（Kind==Atomic）计费；分支 then/else 奖励一律免费。
        /// 退化：未配 Steps 的旧卡仍按扁平 Effects 线性计费（向后兼容）。
        /// 持续/落区/数量/动态/缺陷全部自组合层（def）取（2026-09-10 上移定案）。
        /// </summary>
        private static void VisitBillableAtoms(EffectDefinition effect, int modeIndex,
            Action<AtomicEffectInstance, List<int>> visit)
        {
            // 有效组合域（mode 感知，2026-09-10 极性输入）：per-mode 域优先，回落主序列域
            var domain = effect.ChoiceDomains != null && modeIndex >= 0 && modeIndex < effect.ChoiceDomains.Length
                && effect.ChoiceDomains[modeIndex] != null && effect.ChoiceDomains[modeIndex].Count > 0
                ? effect.ChoiceDomains[modeIndex]
                : effect.TargetDomain;

            if (effect.Steps != null && effect.Steps.Count > 0)
            {
                foreach (var step in effect.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                        visit(step.Atomic, domain);
                    // Kind==Branch：OutcomeGate 奖励免费，不计费。
                    else if (step.Kind == RuntimeStepKind.Choice)
                    {
                        // 抉择（2026-09-07 定案）：各模式独立计价——只计所选模式的原子；
                        // choice 内紧邻分支奖励照旧免费；主序列固定部分（Choice 前后）自然并入每模式。
                        var chosen = step.Choices != null && step.Choices.Count > 0
                            ? step.Choices[Math.Max(0, Math.Min(modeIndex, step.Choices.Count - 1))]
                            : null;
                        if (chosen != null)
                        {
                            foreach (var s in chosen)
                            {
                                if (s == null) continue;
                                if (s.Kind == RuntimeStepKind.Atomic && s.Atomic != null)
                                    visit(s.Atomic, domain);
                                // choice 内 Kind==Branch：奖励免费（converter 已拒嵌套 Choice）
                            }
                        }
                    }
                }
            }
            else if (effect.Effects != null)
            {
                foreach (var atom in effect.Effects)
                {
                    if (atom == null) continue;
                    visit(atom, domain);
                }
            }
        }

        private static void AccumulateElementCost(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, Dictionary<ManaType, int> byColor)
        {
            var cfg = AtomicEffectTable.GetByType(atom.Type);

            // 动态数量（组合层）：费用计 0（代价 = 该卡不可作地牌产元素，见 ElementPool.AddCardToPool）。
            if (def.DynamicTargetCount)
            {
                AccumulateSubEffects(atom, def, domain, byColor);
                return;
            }

            // 突袭的「−1 对冲激励锚价」特判已删（2026-09-11 代价可选定案）：
            // 自上紊乱挪进代价栏（CostType.SelfSickness）——使用时选择窗口决定 减费/得黑/不付，
            // 计价侧不再有效果内对冲；RushSickness 原子留在效果栏会被内容契约剔除（错边只能进代价栏）。

            int amount = ComputeAtomCost(atom, def, domain, cfg);
            if (amount > 0)
            {
                var color = ElementAffinities.GetAffinityForEffect(atom.Type).PrimaryColor;
                byColor.TryGetValue(color, out var prev);
                byColor[color] = prev + amount;
            }

            AccumulateSubEffects(atom, def, domain, byColor);
        }

        /// <summary>
        /// 单个原子的元素费用：
        /// 检索＝筛选维度系数；其余＝round(BaseCost×CostMultiplier×max(1,Value)×持续折扣)，
        /// 固定数量再 ×N；抽牌按所挂减费缺陷累减（下限 0）。
        ///
        /// 持续时间计价（2026-09-10 上移定案后）：持续唯一真相在组合层（def.Duration），
        /// 折扣 = D(def.Duration)/D(Once)——**Once 为绝对锚**（Once 族系数恒 1，
        /// 锚点 1 伤害=1 元素不漂移）；配置表数值不动，比值的分母从「各原子表默认」
        /// 固定为 Once（旧相对口径随表 DurationType 列消亡）。
        /// 法术宿主永久档语义由迁移回填承载（赋予族效果 Duration=Permanent）。
        /// </summary>
        private static int ComputeAtomCost(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, AtomicEffectConfig cfg)
        {
            int amount = ComputeAtomBaseAmount(atom, def, cfg);
            if (amount <= 0) return 0;

            // 错边拆分（2026-09-11 定案，取代错边折价）：有益原子(p>0)锁对方域 / 有害原子(p<0)锁己方域
            // → 计费份额 ×(1−|p|)（当前表 |p|=1 即全额出计价），grant 份额 ×|p| 改发黑/白元素
            //（构筑期显示 DeriveElementGrants / 运行时结算发放见 EffectExecutionEngine）。
            // 双侧域=玩家可选按最优边全价；无目标/p=0 不拆。
            float polarity = atom.Polarity;
            if (polarity != 0f && WrongSide(polarity, domain))
                amount = (int)Math.Round(amount * (1f - Math.Abs(polarity)), MidpointRounding.AwayFromZero);

            // 固定数量：费用 ×N（N=组合层 TargetCount；全部/任意语义无法在构建期确定，按 1）。
            int n = def.TargetCount;
            if (n > 1) amount *= n;

            // 抽牌减费缺陷（组合层）：每挂一个按 BranchConfig.json 给的减免累减，下限 0。
            if (atom.Type == AtomicEffectType.DrawCard && def.Drawbacks != null)
            {
                foreach (var dbId in def.Drawbacks)
                {
                    if (string.IsNullOrEmpty(dbId)) continue;
                    var db = BranchConfigTable.GetDrawback(dbId);
                    if (db != null) amount -= db.CostReduction;
                }
                if (amount < 0) amount = 0;
            }

            return amount;
        }

        /// <summary>
        /// 原子基础量（错边拆分/×N/抽牌缺陷之前的单价）：检索按筛选维度档，其余通用公式，含衍生物落区系数。
        /// 计费（ComputeAtomCost）与黑白获得（ComputeAtomUnitGrant）共用，保证同源不漂移。
        /// </summary>
        private static int ComputeAtomBaseAmount(AtomicEffectInstance atom, EffectDefinition def, AtomicEffectConfig cfg)
        {
            // 检索：按筛选维度档计费，替代通用公式。维度档存于 atom.StringValue（默认单一维度）。
            if (atom.Type == AtomicEffectType.SearchDeck)
                return FilterPrecisionCost(atom);

            if (cfg == null || cfg.BaseCost <= 0f)
                return 0;

            // CostMultiplier 在表加载时默认 1.0；目标范围等可在配置中放大费用。
            float multiplier = cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f;
            int magnitude = Math.Max(1, atom.Value);

            var attrCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
            float durationFactor = attrCfg.GetDurationDiscount(def.Duration, def.DurationValue)
                                   / attrCfg.GetDurationDiscount(DurationType.Once);

            int amount = (int)Math.Round(cfg.BaseCost * multiplier * magnitude * durationFactor, MidpointRounding.AwayFromZero);

            // 衍生物落区系数（P1 定案）：按落区分档计价（战场基准/手牌溢价/牌组微溢价）。
            // 落区在组合层（def.SummonDropZone，显式三档——Zone.Hand==0 陷阱随显式声明消亡）。
            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var dropCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().SummonDropConfig;
                amount = (int)Math.Round(amount * dropCfg.GetFactor(def.SummonDropZone), MidpointRounding.AwayFromZero);
            }

            return amount;
        }

        // ======================================== 黑白元素获得（2026-09-11 定案） ========================================

        /// <summary>错边判定的极性→获得颜色：有害原子(p&lt;0)命中己方→黑；有益原子(p&gt;0)命中对方→白。</summary>
        public static ManaType PolarityGrantColor(float polarity)
            => polarity < 0f ? ManaType.Black : ManaType.White;

        /// <summary>错边判定：极性与域侧别冲突（p&gt;0 且对方锁 / p&lt;0 且己方锁）。双侧/无目标不判错边。</summary>
        public static bool WrongSide(float polarity, List<int> domain)
        {
            int side = SideLock(domain);
            return (polarity > 0f && side == 1) || (polarity < 0f && side == -1);
        }

        /// <summary>
        /// 原子错边黑白获得量——**单目标份额**（未 ×N、未封顶；数量与原错边折价一致 = 单价×|p|）。
        /// 运行时结算发放用：单价 × 实际错边命中数，再按发放事件封顶（地牌上限）。
        /// 实际侧别由调用方按已解析目标判定（非域锁），双域卡实际打错边同样适用。
        /// </summary>
        public static int ComputeAtomUnitGrant(AtomicEffectInstance atom, EffectDefinition def)
        {
            if (atom == null || def == null) return 0;
            float polarity = atom.Polarity;
            if (polarity == 0f) return 0;

            var cfg = AtomicEffectTable.GetByType(atom.Type);
            int baseAmount = ComputeAtomBaseAmount(atom, def, cfg);
            if (baseAmount <= 0) return 0;
            return (int)Math.Round(baseAmount * Math.Abs(polarity), MidpointRounding.AwayFromZero);
        }

        /// <summary>Payload 效果型代价的全价（单目标，未封顶）：按 Once/单目标/战场落区的合成组合层计价。
        /// 零极性原子（无错边概念的代价）按全基础量计——代价本身即牺牲，颜色按域侧判（见补偿服务）。</summary>
        public static int PayloadUnitGrant(AtomicEffectInstance atom)
        {
            if (atom == null) return 0;
            if (atom.Polarity != 0f)
                return ComputeAtomUnitGrant(atom, PayloadGrantDef);

            var cfg = AtomicEffectTable.GetByType(atom.Type);
            return ComputeAtomBaseAmount(atom, PayloadGrantDef, cfg);
        }

        /// <summary>Payload 计价合成组合层（Duration=Once、TargetCount=1、战场落区基准）。</summary>
        private static readonly EffectDefinition PayloadGrantDef = new EffectDefinition
        {
            Id = "COST_PAYLOAD",
            Duration = DurationType.Once,
            TargetCount = 1,
            SummonDropZone = Zone.Battlefield,
        };

        /// <summary>构筑期 grant 累计（DeriveElementGrants 的访问器）：域锁错边原子入桶，子效果同口径递归。</summary>
        private static void AccumulateElementGrant(AtomicEffectInstance atom, EffectDefinition def, List<int> domain,
            Dictionary<ManaType, int> grants)
        {
            // 动态数量：构筑期不可知（计费同口径为 0），运行时按实际命中发放。
            if (def.DynamicTargetCount) return;

            float polarity = atom.Polarity;
            if (polarity != 0f && WrongSide(polarity, domain))
            {
                int unit = ComputeAtomUnitGrant(atom, def);
                if (unit > 0)
                {
                    // 固定数量同计费口径 ×N（构筑显示的声明意图；运行时按实际命中数）
                    int n = def.TargetCount;
                    if (n > 1) unit *= n;

                    var color = PolarityGrantColor(polarity);
                    grants.TryGetValue(color, out var prev);
                    grants[color] = prev + unit;
                }
            }

            // 元/复合效果的子效果同样判定（同 AccumulateSubEffects 口径）。
            if (atom.SubEffects == null) return;
            foreach (var sub in atom.SubEffects)
            {
                if (sub == null) continue;
                AccumulateElementGrant(sub, def, domain, grants);
            }
        }

        private static int FilterPrecisionCost(AtomicEffectInstance atom)
        {
            // 检索改宣言卡名（2026-09-03 定案）：StringValue = 宣言的卡名（构筑期预置），
            // 精度恒为按名称检索（ExactCard 单档=3）；未预置宣言名视为最低档 1。
            if (string.IsNullOrEmpty(atom.StringValue)) return 1;
            var tier = BranchConfigTable.GetFilterTier("ExactCard");
            return tier != null ? tier.Cost : 3;
        }

        private static void AccumulateSubEffects(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, Dictionary<ManaType, int> byColor)
        {
            // 元/复合效果的子效果同样计入费用。
            if (atom.SubEffects == null) return;
            foreach (var sub in atom.SubEffects)
            {
                if (sub == null) continue;
                AccumulateElementCost(sub, def, domain, byColor);
            }
        }

        /// <summary>域侧别锁定（极性折价输入）：-1=纯己方锁 / +1=纯对方锁 / 0=双侧或无目标（可选最优边，不折）。</summary>
        public static int SideLock(List<int> domain)
        {
            if (domain == null || domain.Count == 0) return 0;
            bool own = false, enemy = false;
            foreach (var k in domain)
            {
                if (TargetKindRules.IsEnemySide(k)) enemy = true;
                else own = true;
            }
            if (own && !enemy) return -1;
            if (enemy && !own) return 1;
            return 0;
        }

        /// <summary>
        /// 卡牌是否含「动态数量」效果（组合层标志，2026-09-10 上移）。
        /// 含动态数量的卡费用计 0 且不可作地牌产元素（灵活使用的代价）。
        /// </summary>
        public static bool HasDynamicTargetEffect(CardData card)
        {
            if (card?.Effects == null) return false;
            foreach (var eff in card.Effects)
                if (eff != null && eff.DynamicTargetCount)
                    return true;
            return false;
        }

        /// <summary>卡牌是否含抉择（Choice）步骤（任一效果的 Steps 含 kind==2 且 choices≥2）。</summary>
        public static bool HasChoiceEffect(CardData card)
        {
            return GetModeCount(card) > 1;
        }

        /// <summary>
        /// 抉择模式数（所有 Choice 步骤的最大 choices 数；无抉择返回 1）。
        /// 同卡多个 Choice 步骤共享卡级 ModeIndex，数量不一致时各自 Clamp + 装载警告。
        /// </summary>
        public static int GetModeCount(CardData card)
        {
            int max = 1;
            if (card?.Effects == null) return max;
            foreach (var eff in card.Effects)
            {
                if (eff?.Steps == null) continue;
                foreach (var step in eff.Steps)
                {
                    if (step?.choices != null && step.choices.Count > max)
                        max = step.choices.Count;
                }
            }
            return max;
        }
    }
}
