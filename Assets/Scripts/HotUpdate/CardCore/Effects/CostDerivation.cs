using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 费用自动推导服务 —— 效果「锚价」（即时价，法术定价：第 1 回合立刻打出来的价格）。
    /// 由效果配置表（AttributeValueConfig.json 的 ManaList 位置数组，下标=ManaType 枚举序号）推导出
    /// 「元素消耗代价」，使费用成为配置表唯一权威：费用 = round(份额 × CostMultiplier × 效果值) 按色分配。
    /// 卡牌仍可在 effect.Costs 中声明效果型代价（Payload 原子——弃牌/送墓/流失等资源支付，付费步执行+补偿）。
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
        public static ElementCost DeriveElementCosts(EffectDefinition effect, int modeIndex = 0)
        {
            var byColor = new Dictionary<ManaType, int>();
            if (effect != null)
            {
                VisitBillableAtoms(effect, modeIndex,
                    (atom, domain) => AccumulateElementCost(atom, effect, domain, byColor));

                // 分支计价（2026-09-15 用户定案，**废除 09-13 全部灰费**；2026-10-05 两槽载荷化）：
                // **有限分支（局面门）与产出条件（Outcome）=纯校验上限，零计价**——条件"预算"只是
                // Then 奖励锚价的放置上限（drop 硬校验），奖励原子免费（条件性即折扣），
                // 分支整体贡献 0 费（诅咒门同口径：Then=抽到时的专属载荷）。
                // **引擎（Engine 载荷）=零计价**——拼点门槛=奖励锚价合计（运行时判差额 ≥ 门槛，见 BranchEngines），
                // 奖励按声明值结算（门槛制）；运势 x=纯概率门槛（掷骰阈值）；倒计时延迟即付费。
                // Then 奖励原子免费（VisitBillableAtoms 只扫主序列主干，Branch 载荷不进遍历）。
                // 赋予引擎（Grant）已解体——无条件赋予=无分支槽原子照常主序列计费（×持续档）。
            }

            // 2026-10-04 费用位置数组化：输出统一为 ElementCost（序数序天然确定，非零即入）
            var result = new ElementCost();
            foreach (var kv in byColor)
            {
                if (kv.Value > 0) result[kv.Key] = kv.Value;
            }
            return result;
        }

        /// <summary>
        /// 推导一个效果定义的黑白元素获得量（2026-09-11 定案：错边原子出计价改发元素）。
        /// 有害原子(p&lt;0)锁己方域 → 黑；有益原子(p&gt;0)锁对方域 → 白；双侧域/无目标构筑期不记
        /// （运行时按实际命中错边目标照发，见 EffectExecutionEngine）。
        /// 数量与原错边折价一致：×|p|（当前表 |p|=1 即全额）；固定数量 ×N 同计费口径。
        /// 动态数量原子构筑期不可知，不记（运行时实判）。
        /// </summary>
        public static ElementCost DeriveElementGrants(EffectDefinition effect, int modeIndex = 0)
        {
            var grants = new Dictionary<ManaType, int>();
            if (effect != null)
            {
                VisitBillableAtoms(effect, modeIndex,
                    (atom, domain) => AccumulateElementGrant(atom, effect, domain, grants));
            }
            var result = new ElementCost();
            foreach (var kv in grants)
                if (kv.Value > 0) result[kv.Key] = kv.Value;
            return result;
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
            var domain = ResolveVisitDomain(effect, modeIndex);

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

        /// <summary>计价访问域（per-mode 优先回落主序列域——VisitBillableAtoms 共用）。</summary>
        private static List<int> ResolveVisitDomain(EffectDefinition effect, int modeIndex)
            => effect.ChoiceDomains != null && modeIndex >= 0 && modeIndex < effect.ChoiceDomains.Length
               && effect.ChoiceDomains[modeIndex] != null && effect.ChoiceDomains[modeIndex].Count > 0
                ? effect.ChoiceDomains[modeIndex]
                : effect.TargetDomain;

        private static void AccumulateElementCost(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, Dictionary<ManaType, int> byColor)
        {
            var cfg = ResolvePricingConfig(atom); // 行身份（2026-10-03）：变体行按 RowHashId 取锚

            // 动态数量（组合层）：费用计 0（2026-09-21 定案：不再关联地牌资格——地牌只看生物身份）。
            if (def.TargetCount == -1)
            {
                AccumulateSubEffects(atom, def, domain, byColor);
                return;
            }

            // 突袭的「−1 对冲激励锚价」特判已删（2026-09-11 代价可选定案）：
            // （历史注：突袭曾以 CostType.SelfSickness 代价承载，2026-09-14 代价原子化后已退役）
            // 计价侧不再有效果内对冲；RushSickness 原子留在效果栏会被内容契约剔除（错边只能进代价栏）。

            // ManaList 分色计价（2026-09-14）：标量总价按表行份额比例拆分到各色（规则全保留）
            var byColorCosts = ComputeAtomCostByColor(atom, def, domain, cfg);
            foreach (var kv in byColorCosts)
            {
                if (kv.Value <= 0) continue;
                byColor.TryGetValue(kv.Key, out var prev);
                byColor[kv.Key] = prev + kv.Value;
            }

            AccumulateSubEffects(atom, def, domain, byColor);
        }

        /// <summary>
        /// 单个原子的元素费用：
        /// 检索＝筛选维度系数；其余＝round(BaseCost×CostMultiplier×max(1,Value)×持续折扣)，
        /// 固定数量再 ×N（减费已归代价 Payload 体系——抽牌缺陷累减 2026-09-16 退役）。
        ///
        /// 持续时间计价（2026-09-10 上移定案后）：持续唯一真相在组合层（def.Duration），
        /// 折扣 = D(def.Duration)/D(Once)——**Once 为绝对锚**（Once 族系数恒 1，
        /// 锚点 1 伤害=1 元素不漂移）；配置表数值不动，比值的分母从「各原子表默认」
        /// 固定为 Once（旧相对口径随表 DurationType 列消亡）。
        /// 法术宿主永久档语义由迁移回填承载（赋予族效果 Duration=Permanent）。
        /// </summary>
        /// <summary>全部档（TargetCount≤0）的期望目标数（2026-09-13 用户定案：少了亏多了赚，
        /// 前期很难超过 4 个生物同时存活）。</summary>
        // 固有全域原子（SweepDamage/SweepHeal）2026-09-21 退役——全域语义由组合期
        // TargetKinds+全取档表达，计价统一走"全部"档系数，无 ×1 特判。

        /// <summary>计价档位系数（表 Category=PricingTier，ValueSystemConfig.json）——目标数量/作用次数
        /// 增量系数统一配置（2026-10-05 档位化：数量 1:1/2:1.5/3:2/全部:3；次数 1:1/2:1.5/3:2/无上限:4）；
        /// 文件缺失/未灌入走字段初始化器默认（与表同值）。</summary>
        private static PricingTierConfig Tiers =>
            ValueSystemConfigManager.Instance.GetOrCreateConfig().PricingTierConfig;

        /// <summary>数量系数（2026-10-05 档位化，表 PricingTier）：SummonToken=1（2026-10-07 表价清零：
        /// 专项计价=模板费×数量，数量不外乘）；全取档（Whole/WholeUnion，2026-09-16 六值迁移）按"全部"档
        /// （TargetCount 是 converter 兜底噪声，不代表真实目标数）；其余按 TargetCount 档
        /// （-1 任意同全部档；-2 未声明=单目标基准）。原"整数 ×N / 期望 4"口径退役。</summary>
        private static float QuantityFactor(AtomicEffectType type, EffectDefinition def)
        {
            if (type == AtomicEffectType.SummonToken) return 1f;
            if (SelectionModeRules.IsTakeAll(def.SelectionMode)) return Tiers.TargetCountFactor(0);
            return Tiers.TargetCountFactor(def.TargetCount);
        }

        /// <summary>属性锚（2026-09-13 定案；2026-10-05 迁表 CardCost.StatAnchor）：+1 攻/+1 生命 = 0.5（攻血同锚）。</summary>
        public static float StatAnchor => ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig.StatAnchor;

        /// <summary>
        /// 属性价梯（修改/改写族——ModifyPower/ModifyLife/ModifyCost/Set*，非指示物原子）：
        /// 返回该原子在当前持续档的每 +1 单价；非本族返回 0（走通用公式/指示物统一计价）。
        /// 修改族 0.5/1.5（固定1回合/换区档）；改写族（Set* 设置直改）恒 3.0；
        /// 指示物原子（Add* 等）不走此梯——2026-10-08 层改造定案：基础费用×赋予层数（见 ComputeAtomBaseAmount）。
        /// </summary>
        public static float StatTierPrice(AtomicEffectType type, EffectDefinition def)
        {
            bool isSet = type == AtomicEffectType.SetPower || type == AtomicEffectType.SetLife;
            bool isModify = type == AtomicEffectType.ModifyPower || type == AtomicEffectType.ModifyLife;
            // 费用修改两档（2026-09-13 定案）：指示物档（CostUp/Down 计数——仅手牌离手消失=换区语义）；
            // 永久改写 3.0/+1（Permanent=直改本体）。乘数 2026-10-05 迁表 CardCost。
            var cc = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig;
            if (type == AtomicEffectType.ModifyCost)
                return def.Duration == DurationType.Permanent ? cc.StatRewriteFlatCost : StatAnchor * cc.StatSustainMultiplier;
            if (!isSet && !isModify) return 0f;
            if (isSet) return cc.StatRewriteFlatCost; // 改写档（设置直改视同本体）恒 3.0/+1

            float per = StatAnchor;
            switch (def.Duration)
            {
                case DurationType.UntilEndOfTurn: return per;                    // 固定1回合 0.5
                case DurationType.UntilNextTurn: return per;                     // ≡1回合（2026-09-16 统一档）
                case DurationType.UntilLeaveBattlefield: return per * cc.StatSustainMultiplier;  // 换区移除 1.5
                case DurationType.WhileCondition: return per * cc.StatSustainMultiplier;         // 条件持续≈换区档
                case DurationType.Permanent: return per * cc.StatPermanentMultiplier;            // 设置轨直写档 2.0
                default: return per; // Once 等瞬态兜底（属性 grant 不应出现）
            }
        }

        /// <summary>指示物行为三型标签（2026-10-08 层改造定案——原子表 Tags 携带，与 CounterSpec.Class 同源）。
        /// 计价判定用：携带任一标签的原子=指示物族，走「基础费用×赋予层数」统一计价。</summary>
        private static readonly HashSet<string> CounterBehaviorTags =
            new HashSet<string> { "衰退型", "换区清除型", "生效自减型" };

        /// <summary>是否指示物族原子（表行 Tags 携带行为三型之一）。</summary>
        internal static bool IsCounterAtom(AtomicEffectConfig cfg)
            => cfg != null && cfg.GetTagList().Any(CounterBehaviorTags.Contains);

        /// <summary>固定分支门预算表（2026-09-14 定案：门=纯校验上限，零计价——奖励原子维持 0 费，
        /// 预算只是放置上限；"造成伤害时"门已随战斗伤害改写族上线而移除）。
        /// 2026-09-22 新增局面状态族 8 门（通用门，预算 1）——零计价口径不变。
        /// 2026-10-05 新增诅咒门（CurseOnDraw，预算 2）——Then 原子=抽到时的专属载荷。
        /// 改写门（DmgRewrite*）已随四条改写迁唯一光环退役。</summary>
        public static readonly Dictionary<string, int> GatePremium = new Dictionary<string, int>
        {
            { "DmgKillsTarget", 2 }, // 消灭目标时
            { "DeclareHit", 2 },     // 宣言结果一致时
            // ---- 产出条件·补录（2026-10-05 改归自由分支·产出条件族）----
            { "DeclareMiss", 2 },    // 宣言落空时（与命中对称）
            { "ProphecyHit", 2 },    // 预言命中时（延迟验证：对手下回合首张出牌结算）
            { "ProphecyMiss", 2 },   // 预言落空时（延迟验证，语义反转）
            // ---- 局面状态族（2026-09-22，预算 1）----
            { "DrawnInStandbyThisTurn", 1 }, // 本回合准备阶段抽到的卡
            { "LifeBelowOpp", 1 },           // 生命值低于对手
            { "LifeAboveOpp", 1 },           // 生命值高于对手
            { "DeckBelowOpp", 1 },           // 卡组剩余低于对手
            { "DeckAboveOpp", 1 },           // 卡组剩余高于对手
            { "CreaturesBelowOpp", 1 },      // 场上生物低于对手
            { "CreaturesAboveOpp", 1 },      // 场上生物高于对手
            { "FirstCardThisTurn", 1 },      // 本回合使用的第一张卡
            // ---- 局面状态族·二批（2026-09-22 追加，预算 2）----
            { "LandsGe7", 2 },               // 操控地数量≥7
            { "HandEmpty", 2 },              // 手牌数量=0
            { "LifeLe7", 2 },                // 生命值≤7
            // ---- 诅咒门（2026-10-05 定案）----
            { "CurseOnDraw", 2 },            // 抽到该卡时（时机固定）——诅咒载荷 ≤2 费
        };

        /// <summary>奖励原子的推导费合计（2026-09-14 自 CardEffectConverter 上移——倒计时回合换算与
        /// 合成器【奖励x】预算校验共用同一口径）：单次/单目标锚价——Once、TargetCount=1、
        /// TriggerLimitPerTurn=1 的 shim（防字段默认 -1 被 TriggerCostFactor 当"无上限"档、
        /// TargetCount=0 落"全部"档的膨胀——2026-09-13 修复口径固化于此）。</summary>
        public static float RewardDerivedCost(List<AtomicEffectInstance> atoms)
        {
            if (atoms == null || atoms.Count == 0) return 0f;
            var shim = new EffectDefinition { Id = "REWARD_SHIM", Duration = DurationType.Once,
                TriggerLimitPerTurn = 1, TargetCount = 1 };
            shim.Effects = atoms;
            return DeriveElementCosts(shim).Total;
        }

        /// <summary>触发上限计价系数（2026-10-05 档位化，表 PricingTier：1:1 / 2:1.5 / 3:2 / 无上限:4；
        /// 原 1.2^(N-1) 连乘 / ×1.2³ 口径退役）。N&gt;3 视同无上限；N=1 / 非触发式（启动式现付、光环静态）不乘。
        /// 只对触发式生效（IsTriggeredEffect 守卫）。</summary>
        public static float TriggerCostFactor(EffectDefinition def)
        {
            if (def == null || !def.IsTriggeredEffect) return 1f;
            return Tiers.TriggerLimitFactor(def.TriggerLimitPerTurn);
        }

        private static int ComputeAtomCost(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, AtomicEffectConfig cfg)
        {
            int amount = ComputeAtomBaseAmount(atom, def, cfg);
            if (amount <= 0) return 0;

            // 错边拆分（2026-09-11 定案，取代错边折价）：有益原子(p>0)锁对方域 / 有害原子(p<0)锁己方域
            // → 计费份额 ×(1−|p|)（当前表 |p|=1 即全额出计价），grant 份额 ×|p| 改发黑/白元素
            //（构筑期显示 DeriveElementGrants / 运行时结算发放见 EffectExecutionEngine）。
            // 双侧域=玩家可选按最优边全价；无目标/p=0 不拆。
            float polarity = atom.Polarity;
            if (polarity != 0f && !IsSelfSleepExempt(atom.Type, domain) && WrongSide(polarity, domain))
                amount = (int)Math.Round(amount * (1f - Math.Abs(polarity)), MidpointRounding.AwayFromZero);

            // 目标数量计价（2026-10-05 档位化，表 PricingTier：1:1 / 2:1.5 / 3:2 / 全部:3）；
            // 全部/任意语义无法在构建期确定——按"全部"档计（原期望 4 口径退役）。
            // 固有全域原子（类型伤害/全体治疗）范围溢价已含 BaseCost，数量恒 ×1。
            // 无目标域（2026-10-03 规则光环配套）：数量是目标数量语义，无目标可乘——恒 ×1
            //（否则规则光环按表锚价虚高"全部"档倍数）。
            float n = QuantityFactor(atom.Type, def);
            if ((domain == null || domain.Count == 0) && n > 1f)
                n = 1f;
            if (n > 1f)
                amount = (int)Math.Round(amount * n, MidpointRounding.AwayFromZero);

            // 触发上限计价（2026-10-05 档位化，表 PricingTier：1:1 / 2:1.5 / 3:2 / 无上限:4；
            // 原 1.2 连乘口径退役）；N=1 / 非触发式 / 光环不乘。
            float triggerFactor = TriggerCostFactor(def);
            if (triggerFactor > 1f)
                amount = (int)Math.Round(amount * triggerFactor, MidpointRounding.AwayFromZero);

            // 双方同时作用减半（2026-10-03 用户定案；2026-10-07 规则光环范围化）：效果同时作用于双方——
            // 双侧域 + 全取档/显式多目标（双方全体恢复/双方各消灭一生物），或规则光环的作用范围=双方
            //（ModifyGameRule 的 Value=RuleAuraScope.Both）——计费 ×0.5（对称面让利）；
            // 规则光环选己方/对方单侧=单侧锁口径不減；双侧域单体任选一侧同不減。
            if (IsSymmetricBothSides(atom, def, domain))
                amount = (int)Math.Round(amount * SymmetricDiscountFactor, MidpointRounding.AwayFromZero);

            return amount;
        }

        /// <summary>双方同时作用减半系数（2026-10-03 用户定案；2026-10-05 迁表 CardCost.SymmetricDiscountFactor）。</summary>
        public static float SymmetricDiscountFactor => ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig.SymmetricDiscountFactor;

        /// <summary>
        /// 是否「同时作用双方」（2026-10-03 用户定案减半口径；2026-10-07 规则光环范围化）：
        /// - 无目标域 + ModifyGameRule（规则光环）——仅当作用范围=双方（Value==RuleAuraScope.Both；
        ///   己方/对方单侧=单侧锁口径全价）；
        /// - 双侧域（SideLock=0 且域非空）且 全取档（SelectionModeRules.IsTakeAll）或显式声明多目标
        ///   （TargetCount≥2——哨兵 -2/0=未声明「任意」不算，防宽域单体卡误减）。
        /// 单侧锁（可按最优边全价）与双侧域单体任选一侧均不減。
        /// </summary>
        public static bool IsSymmetricBothSides(AtomicEffectInstance atom, EffectDefinition def, List<int> domain)
        {
            if (atom != null && atom.Type == AtomicEffectType.ModifyGameRule
                && (domain == null || domain.Count == 0))
                return atom.Value == (int)RuleAuraScope.Both;
            if (domain == null || domain.Count == 0) return false;
            if (SideLock(domain) != 0) return false;
            return SelectionModeRules.IsTakeAll(def != null ? def.SelectionMode : 0)
                   || (def != null && def.TargetCount >= 2);
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

            // 召唤衍生物（2026-10-07 表价清零定案）：单价随模板——衍生物卡自身费用×召唤数量，
            // 表行 ManaList 已清零不再携带锚价（置于零价守卫之前，恒出模板价）。
            // 模板不可解析/零费=0（构筑期校验拦截、运行时 handler 兜底拒绝）；
            // 数量/持续档不乘（量级即最终口径），触发档仍由外层统一乘。
            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var resolver = Attribute.Handlers.SummonTokenHandler.ResolveTemplate ?? Attribute.MorphSystem.ResolveMorphTarget;
                var template = string.IsNullOrEmpty(atom.StringValue) ? null : resolver?.Invoke(atom.StringValue);
                if (template == null || template.TotalCost <= 0f) return 0;
                int count = Math.Max(1, atom.Value);
                var dropCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().SummonDropConfig;
                return (int)Math.Round(template.TotalCost * count * dropCfg.GetFactor(Zone.Battlefield),
                    MidpointRounding.AwayFromZero);
            }

            // 属性价梯（2026-09-13 定案：攻/血同锚 0.5/+1，按持续档定价——取代通用公式与持续折扣）：
            // 修改族（ModifyPower/ModifyLife）档价：固定1回合（UntilEndOfTurn/UntilNextTurn）0.5
            //（2026-09-16 统一档：限时指示物两档合一=持有者回合结束，费用一律按 1 回合计）、
            // 换区移除（UntilLeaveBattlefield/WhileCondition）1.5、换区不移除（Permanent=指示物永久档）2.0；
            // 改写族（SetPower/SetLife=设置直改，视同本体）3.0。
            float statTier = StatTierPrice(atom.Type, def);
            if (statTier > 0f)
                return (int)Math.Round(statTier * Math.Abs(atom.Value), MidpointRounding.AwayFromZero);

            // 指示物统一计价（2026-10-08 层改造定案）：**基础费用 × 赋予层数**（线性，砍档位梯）——
            // 基础费用=表行 ManaList 锚（即 1 层价，量级回归表行）；层数=max(1,|value|)
            //（未开放 value 的行统一按 1 层计）。判定=表行 Tags 行为三型（衰退型/换区清除型/生效自减型，
            // 与 CounterSpec.Class 同源）——原属性指示物锚梯（StatCounterTierPrice 1.5/层）随之退役。
            if (IsCounterAtom(cfg))
                return (int)Math.Round(cfg.TotalUnitCost * Math.Max(1, Math.Abs(atom.Value)),
                    MidpointRounding.AwayFromZero);

            // 控制权三档（2026-09-13 定案）：回合级临时（UET/UNT/ForTurns≤2）×1.2 /
            // 持续到离场（ULB/WhileCondition）×1.6 / 改写持有者（Permanent——控制+owner 换写，
            // 弹回洗回死亡都归新主）×3.0。ChangeOwner 显式原子恒 ×3.0（同第三档）。
            if (atom.Type == AtomicEffectType.GainControl || atom.Type == AtomicEffectType.ChangeOwner)
            {
                float ctrlMult = def.Duration == DurationType.Permanent || atom.Type == AtomicEffectType.ChangeOwner ? 3f
                    : (def.Duration == DurationType.UntilLeaveBattlefield || def.Duration == DurationType.WhileCondition) ? 1.6f
                    : 1.2f;
                return (int)Math.Round(cfg.TotalUnitCost * ctrlMult, MidpointRounding.AwayFromZero);
            }

            // Grant 关键词梯（2026-09-13 定案；2026-09-16 统一档：UNT≡UET 计价并入 1.2 档）：
            // 一次性（Once，圣盾/复生式消耗）×1.0 / 临时（UET/UNT——持续到持有者回合结束）×1.2 /
            // 换区持续（ULB/WhileCondition）×1.6 / 永久（Permanent——Setting 回填）×2.0。
            if (atom.Type.ToString().StartsWith("Grant"))
            {
                float grantMult;
                switch (def.Duration)
                {
                    case DurationType.Once: grantMult = 1.0f; break;
                    case DurationType.UntilEndOfTurn:
                    case DurationType.UntilNextTurn: grantMult = 1.2f; break;
                    case DurationType.UntilLeaveBattlefield:
                    case DurationType.WhileCondition: grantMult = 1.6f; break;
                    default: grantMult = 2.0f; break; // Permanent（含魔法 Setting 回填）
                }
                return (int)Math.Round(cfg.TotalUnitCost * grantMult, MidpointRounding.AwayFromZero);
            }

            if (cfg == null || cfg.TotalUnitCost <= 0f)
                return 0;

            // 规则光环（2026-10-07 范围化）：Value=作用范围序（RuleAuraScope）非量级——不乘 magnitude，
            // 恒单价×成本乘数×持续档（与旧口径 value=1 等价）；双方让利由 IsSymmetricBothSides 统一裁决
            //（含四负面光环与舍身——2026-10-07 改写降级回滚，改写族 4 箭头计价口径退役）。
            if (atom.Type == AtomicEffectType.ModifyGameRule)
            {
                float ruleMult = cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f;
                var ruleAttr = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
                return (int)Math.Round(cfg.TotalUnitCost * ruleMult
                                       * ruleAttr.GetDurationDiscount(def.Duration)
                                       / ruleAttr.GetDurationDiscount(DurationType.Once),
                    MidpointRounding.AwayFromZero);
            }

            // CostMultiplier 在表加载时默认 1.0；目标范围等可在配置中放大费用。
            float multiplier = cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f;
            int magnitude = Math.Max(1, atom.Value);

            var attrCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
            float durationFactor = attrCfg.GetDurationDiscount(def.Duration)
                                   / attrCfg.GetDurationDiscount(DurationType.Once);

            return (int)Math.Round(cfg.TotalUnitCost * multiplier * magnitude * durationFactor, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 分色计价（2026-09-14 ManaList 定案；2026-10-04 位置数组化）：**先按既有标量管线算总价**
        /// （ComputeAtomCost——含梯价/错边拆分/数量期望/触发连乘/缺陷减费/落区全部规则，一字不改），
        /// 再按表行费用构成份额**比例拆分**到各色（序数序）。单色行=行为与旧口径完全一致；
        /// 混合色行=总价不变、构成按份额分布（余数归序数序末个非零色保总额）。
        /// 例外：召唤衍生物（2026-10-07 表价清零）构成随模板卡费用，表行不再携带份额。
        /// </summary>
        public static Dictionary<ManaType, int> ComputeAtomCostByColor(AtomicEffectInstance atom, EffectDefinition def,
            List<int> domain, AtomicEffectConfig cfg)
        {
            var result = new Dictionary<ManaType, int>();
            int total = ComputeAtomCost(atom, def, domain, cfg);
            var cost = cfg?.ManaList;
            // 召唤衍生物（2026-10-07 表价清零）：表行构成已清零——分色换轨随模板卡费用构成
            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var resolver = Attribute.Handlers.SummonTokenHandler.ResolveTemplate ?? Attribute.MorphSystem.ResolveMorphTarget;
                var template = string.IsNullOrEmpty(atom.StringValue) ? null : resolver?.Invoke(atom.StringValue);
                if (template != null && !template.Cost.IsZero) cost = template.Cost;
            }
            if (total <= 0 || cost == null || cost.IsZero) return result;

            float unitSum = cost.Total;
            if (unitSum <= 0f)
            {
                result[cost.PrimaryColor] = total; // 份额退化（不应发生）——全额落主色
                return result;
            }

            int allocated = 0;
            var colors = cost.NonzeroColors().ToList();
            for (int i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                if (i == colors.Count - 1)
                {
                    // 末个非零色吃余数——保证分配合计 == total（最大余数法的单行简化）
                    result.TryGetValue(color, out var prevLast);
                    result[color] = prevLast + (total - allocated);
                    break;
                }
                int share = (int)Math.Round(total * (cost[color] / unitSum), MidpointRounding.AwayFromZero);
                result.TryGetValue(color, out var prev);
                result[color] = prev + share;
                allocated += share;
            }
            return result;
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

        /// <summary>代价栏有效域计算（2026-10-04 定案：任意单向效果·放入代价栏后逆转选择范围）。
        /// p≠0 强制错误侧（p&lt;0→己方 / p&gt;0→对方）：表域双侧→收窄到错误侧成员；表域恰为整侧→免写(null)；
        /// 表域无错误侧成员（单侧在正确侧）→<see cref="TargetKindRules.MirrorDomain"/> 镜像逆转；
        /// 镜像后仍不满足装载契约（如 p&gt;0 纯 {Self} 域——Self 无对侧）= 不可入。
        /// p=0 中性须表域单侧锁定（双侧/无目标=既非代价也非收益，拒）。
        /// 返回 (kinds, eligible)：kinds=null 表示免写（表默认域同效）；合成器放置口与候选过滤共用本口。</summary>
        public static (List<int> kinds, bool eligible) PayloadCostDomain(AtomicEffectConfig cfg)
        {
            if (cfg == null) return (null, false);
            var kinds = cfg.GetTargetKindList();
            float p = Math.Clamp(cfg.Polarity, -1f, 1f);
            if (p == 0f)
                return SideLock(kinds) != 0 ? (null, true) : (null, false);

            bool wantEnemy = p > 0f;
            var side = kinds.Where(k => TargetKindRules.IsEnemySide(k) == wantEnemy).ToList();
            if (side.Count > 0)
                return side.Count == kinds.Count ? (null, true) : (side, true);

            var mirrored = TargetKindRules.MirrorDomain(kinds); // 正确侧单向 → 逆转
            return WrongSide(p, mirrored) ? (mirrored, true) : (null, false);
        }

        /// <summary>自我沉睡豁免（2026-10-02 苏醒退役配套）：Sleep 原子收窄域恰为 {Self} 不作错边——
        /// 沉睡行合并后极性 -1，自我沉睡实例（灰时长/定长两模式）是既定效果栏机制，
        /// 其"对自己有害"的代价语义由灰费豁免（GameActions.HasSelfSleepEffect：灰份额剥离转时长）
        /// 替代承担——契约剔除与错边转化（计价 ×(1−|p|) 出计价 / 黑获得）双双跳过，
        /// 自我沉睡按全价绿计、支付时仅剥离灰份额。消费点：CardEffectConverter 契约、
        /// ComputeAtomCost 错边拆分、AccumulateElementGrant 黑白获得。</summary>
        public static bool IsSelfSleepExempt(AtomicEffectType type, List<int> domain)
            => type == AtomicEffectType.Sleep
               && domain != null && domain.Count == 1 && domain[0] == (int)TargetKind.Self;

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

            var cfg = ResolvePricingConfig(atom); // 行身份（2026-10-03）：变体行按 RowHashId 取锚
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

            var cfg = ResolvePricingConfig(atom); // 行身份（2026-10-03）：变体行按 RowHashId 取锚
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
            if (def.TargetCount == -1) return;

            float polarity = atom.Polarity;
            if (polarity != 0f && !IsSelfSleepExempt(atom.Type, domain) && WrongSide(polarity, domain))
            {
                int unit = ComputeAtomUnitGrant(atom, def);
                if (unit > 0)
                {
                    // 固定数量同计费口径（2026-10-05 档位化 / 触发档位——与 ComputeAtomCost 同口径）
                    float n = QuantityFactor(atom.Type, def);
                    if (n > 1f) unit = (int)Math.Round(unit * n, MidpointRounding.AwayFromZero);
                    float gtf = TriggerCostFactor(def);
                    if (gtf > 1f) unit = (int)Math.Round(unit * gtf, MidpointRounding.AwayFromZero);

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

        /// <summary>计价取行（2026-10-03 行身份修复）：同枚举多行（变体行各自锚价——如规则光环 7 行、
        /// 洗回/洗入）时按实例来源行（RowHashId，converter 填）取锚；null/缺行回落 GetByType
        ///（末行覆写，旧口径——手工构造实例与历史路径兼容）。</summary>
        private static AtomicEffectConfig ResolvePricingConfig(AtomicEffectInstance atom)
        {
            if (atom == null) return null;
            if (!string.IsNullOrEmpty(atom.RowHashId))
            {
                var byRow = AtomicEffectTable.GetByHashId(atom.RowHashId);
                if (byRow != null) return byRow;
            }
            return AtomicEffectTable.GetByType(atom.Type);
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

        /// <summary>
        /// 效果槽位数（2026-09-21 抉择分支计槽定案）：每个效果 1 槽，Choice 步骤每多一个分支再 +1 槽
        /// （抉择装两个效果，收两次槽位费）。供 ChassisAdjust 底盘预算消费。
        /// 启动式照常计槽（2026-10-02 用户定案：「不占费用，占技能挂载」维持——效果槽是 AI 侧
        /// 每卡最大原子数限制的可见性锚，免槽会让 AI 看不见超限的启动式堆叠）；
        /// 启动式占用的只是底盘免费额度抵扣位，元素锚价仍构筑期全免（运行时现付）。
        /// 代价栏计 1 槽（2026-10-04 定案：代价也是效果栏，不是特殊判——使用代价栏要单独为
        /// 效果栏付 1 费，底盘 3 灰预算同口径扣槽，超出加价；全价不并入卡费，仅作门槛与补偿基准）。
        /// 卡层 PayloadCost 与 legacy 效果级 Costs 同卡只计 1 槽（单卡单条契约）。
        /// </summary>
        public static int CountEffectSlots(CardData card)
        {
            if (card == null) return 0;
            int slots = 0;
            if (card.Effects != null)
            {
                foreach (var eff in card.Effects)
                {
                    if (eff == null) continue;
                    slots += 1;
                    if (eff.Steps == null) continue;
                    foreach (var step in eff.Steps)
                    {
                        if (step?.choices != null && step.choices.Count > 1)
                            slots += step.choices.Count - 1;
                    }
                }
            }
            if (HasPayloadEntry(card)) slots += 1;
            return slots;
        }

        /// <summary>卡上是否填装了代价（卡层 PayloadCost 正式口 / legacy 效果级 Costs 兜底，任一即真）。</summary>
        private static bool HasPayloadEntry(CardData card)
        {
            if (card.PayloadCost?.payload != null && !string.IsNullOrEmpty(card.PayloadCost.payload.refId))
                return true;
            if (card.Effects == null) return false;
            foreach (var eff in card.Effects)
            {
                if (eff?.Costs == null) continue;
                foreach (var ce in eff.Costs)
                    if (ce?.payload != null && !string.IsNullOrEmpty(ce.payload.refId)) return true;
            }
            return false;
        }
    }
}
