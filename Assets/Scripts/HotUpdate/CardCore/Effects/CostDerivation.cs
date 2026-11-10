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
    /// 卡级组合计价（底盘/结构费/属性分色）不在此处 —— 在 CardCostService 的组合层。
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
            // 锚价钉死（2026-10-20 教学段七配套）：手作行 cost 列为权威——直出不推导
            //（null/空=0 费钉死）；法术施放锚价/合成器预览等全部消费面同口单源。
            // 缺省不钉=实时推导（历史唯一口径，正式表零影响）。
            // 锚价钉死（2026-10-20 教学段七配套）：手作行 cost 列为权威——直出不推导
            //（null/空=0 费钉死）；法术施放锚价/合成器预览等全部消费面同口单源。
            // 缺省不钉=实时推导（历史唯一口径，正式表零影响）。
            if (effect != null && effect.AnchorCostPinned)
                return effect.AnchorCostPin != null ? effect.AnchorCostPin.Clone() : new ElementCost();

            // 逐色累加（float 全程不取整——2026-10-30 真实小数定案，10-18「合成期取整出整数」退役：
            // 本层恒出真值小数；卡层 CardCostService 在代价抵扣前逐色向上取整，支付终点 NormalizeBill
            // 同规则（幂等）；E/K 整数分摊仍在 CardCostService.ApportionMounted（L3））。
            var byColor = new Dictionary<ManaType, float>();
            if (effect != null)
            {
                VisitBillableAtoms(effect, modeIndex, (atom, domain) =>
                {
                    AccumulateElementCost(atom, effect, domain, byColor);

                    // 分支计价（2026-09-15 用户定案，**废除 09-13 全部灰费**；2026-10-05 两槽载荷化）：
                    // **有限分支（局面门）与产出条件（Outcome）=纯校验上限，零计价**——条件"预算"只是
                    // Then 奖励锚价的放置上限（drop 硬校验），奖励原子免费（条件性即折扣），
                    // 分支整体贡献 0 费（不达标不奖励也不惩罚——纯条件奖励）。
                    // **引擎（Engine 载荷）=零计价**——拼点门槛=奖励锚价合计（运行时判差额 ≥ 门槛，见 BranchEngines），
                    // 奖励按声明值结算（门槛制）；运势 x=纯概率门槛（掷骰阈值）；倒计时延迟即付费。
                    // Then 奖励原子免费（VisitBillableAtoms 只扫主序列主干，Branch 载荷不进遍历）。
                    // 赋予引擎（Grant）已解体——无条件赋予=无分支槽原子照常主序列计费（×持续档）。
                    // **例外（2026-10-09 附加指示物定案）**：附加诅咒/附加祝福分支**不奖励免费**——
                    // 按分支实际填入的诅咒/祝福费用减半向上取整入卡价（EngineBranchSurcharge）。
                    if (atom.Branch != null && atom.Branch.Settle == BranchSettleKind.Engine
                        && (atom.Branch.EngineKind == BranchEngineKind.CurseOnDraw
                            || atom.Branch.EngineKind == BranchEngineKind.BlessingOnDraw))
                    {
                        var surcharge = EngineBranchSurcharge(atom.Branch);
                        foreach (var c in surcharge.NonzeroColors())
                            byColor[c] = byColor.TryGetValue(c, out var v) ? v + surcharge[c] : surcharge[c];
                    }
                });
            }

            // 2026-10-04 费用位置数组化：输出统一为 ElementCost（序数序天然确定，非零即入）——
            // 真实小数出账（2026-10-30 定案，逆转 10-18 合成期取整）：效果费保留真值小数（如 0.6绿），
            // 展示层直通；卡层 CardCostService 在代价抵扣前逐色向上取整，支付终点
            //（ElementPaymentValidator.NormalizeBill，大于 0 也消耗 1）同规则（幂等）。
            // 卡级底盘分摊（ApportionMounted L3）不在此处。
            var result = new ElementCost();
            foreach (var kv in byColor)
            {
                if (kv.Value > 0f) result[kv.Key] = kv.Value;
            }
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
                        visit(step.Atomic, PricingDomain(effect, step.Atomic, domain));
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
                                    visit(s.Atomic, PricingDomain(effect, s.Atomic, domain));
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
                    visit(atom, PricingDomain(effect, atom, domain));
                }
            }
        }

        /// <summary>计价访问域（per-mode 优先回落主序列域——VisitBillableAtoms 共用）。</summary>
        private static List<int> ResolveVisitDomain(EffectDefinition effect, int modeIndex)
            => effect.ChoiceDomains != null && modeIndex >= 0 && modeIndex < effect.ChoiceDomains.Length
               && effect.ChoiceDomains[modeIndex] != null && effect.ChoiceDomains[modeIndex].Count > 0
                ? effect.ChoiceDomains[modeIndex]
                : effect.TargetDomain;

        /// <summary>计价域（2026-10-09 逐原子目标制）：逐原子模式按**该原子自身域**计价——
        /// 错边拆分/对称减半/无目标数量豁免都吃域侧别，喂共享并集会让单侧原子误判成双侧（虚减半）；
        /// 共享口径（header 声明）照旧用组合域。域原样不极性过滤（与解析域/旧单原子口径一致）。</summary>
        private static List<int> PricingDomain(EffectDefinition effect, AtomicEffectInstance atom, List<int> shared)
            => effect != null && effect.PerAtomTargets
                ? atom?.TargetKinds ?? new List<int>()
                : shared;

        private static void AccumulateElementCost(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, Dictionary<ManaType, float> byColor)
        {
            var cfg = ResolvePricingConfig(atom); // 行身份（2026-10-03）：变体行按 RowHashId 取锚

            // 动态数量（组合层；2026-10-09 每原子口径：该原子有效数量=任意才零费）：
            // 费用计 0（2026-09-21 定案：不再关联地牌资格——地牌只看生物身份）。
            if (atom != null && atom.EffectiveTargetCount(def) == -1)
            {
                AccumulateSubEffects(atom, def, domain, byColor);
                return;
            }

            // 极性裁定（2026-11-02 定案）：代价型原子指向自己=纯代价——亏牌/自伤本身即代价，
            // 不得计正费（自我沉睡 Self 零费先例的推广）。效果栏贡献 0 费；代价语义的抵扣走
            // 代价栏（payloads/Costs 行 → SumPayloadPrice 色锁，不经本口不受影响）。
            // 判定域=原子自身 kinds 优先（实例指向声明）——计价域 PricingDomain 在效果未声明
            // 共享域时为空，不承载「指向谁」语义。设计意图自查走 SelfCostViolations
            //（重推工具 + 端到端验证双拦截，不静默）。
            if (IsSelfDirectedCostAtom(atom, def))
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
        /// 检索＝筛选维度系数；其余＝BaseCost×CostMultiplier×max(1,Value)×持续折扣
        ///（内部全程小数不取整——出口取整见 DeriveElementCosts 头注〔2026-10-18 效果合成阶段逐色取整〕），
        /// 固定数量再 ×N（减费已归代价 Payload 体系——抽牌缺陷累减 2026-09-16 退役）。
        ///
        /// 持续时间计价（2026-09-10 上移定案后）：持续唯一真相在组合层（def.Duration），
        /// 折扣 = D(def.Duration)/D(Once)——**Once 为绝对锚**（Once 族系数恒 1，
        /// 锚点 1 伤害=1 元素不漂移）；配置表数值不动，比值的分母从「各原子表默认」
        /// 固定为 Once（旧相对口径随表 Duration 列消亡）。
        /// 法术宿主永久档语义由迁移回填承载（赋予族效果 Duration=Permanent）。
        /// </summary>
        /// <summary>全部档（TargetCount≤0）的期望目标数（2026-09-13 用户定案：少了亏多了赚，
        /// 前期很难超过 4 个生物同时存活）。</summary>
        // 固有全域原子（SweepDamage/SweepHeal）2026-09-21 退役——全域语义由组合期
        // TargetKinds+全取档表达，计价统一走"全部"档系数，无 ×1 特判。

        /// <summary>计价档位系数（表 Category=PricingTier，ValueSystemConfig.json）——目标数量增量系数
        ///（2026-10-05 档位化：数量 1:1/2:1.5/3:2/全部:3；作用次数档 2026-10-30 整删）；
        /// 文件缺失/未灌入走字段初始化器默认（与表同值）。</summary>
        private static PricingTierConfig Tiers =>
            ValueSystemConfigManager.Instance.GetOrCreateConfig().PricingTierConfig;

        /// <summary>数量系数（2026-10-05 档位化，表 PricingTier；2026-10-09 每原子下沉）：
        /// SummonToken=1（2026-10-07 表价清零：专项计价=模板费×数量，数量不外乘）；
        /// 全取档（Whole/WholeUnion，2026-09-16 六值迁移）按"全部"档（TargetCount 是 converter
        /// 兜底噪声，不代表真实目标数）；其余按**该原子有效数量档**（实例声明优先，未声明回落效果级；
        /// -1 任意同全部档；-2 未声明=单目标基准）。原"整数 ×N / 期望 4"口径退役。</summary>
        private static float QuantityFactor(AtomicEffectInstance atom, EffectDefinition def)
        {
            if (atom != null && atom.Type == AtomicEffectType.SummonToken) return 1f;
            if (SelectionModeRules.IsTakeAll(def.SelectionMode)) return Tiers.TargetCountFactor(0);
            return Tiers.TargetCountFactor(atom != null ? atom.EffectiveTargetCount(def) : def.TargetCount);
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

        /// <summary>分支条件预算表构建（2026-10-23 有限分支入表定案：静态字典 → 每次构建——
        /// 产出条件 5 项（消灭/宣言命中落空/预言命中落空）预算恒 2 为代码常量，
        /// 局面状态族 11 门并入原子表 Budget 列（表重读即生效，**预算唯一来源=表**——产出条件族除外，
        /// 任何人不得再在代码里手写局面门预算）。
        /// 条件=纯校验上限零计价（2026-09-14 定案）——奖励原子维持 0 费，预算只是放置上限。
        /// 改写门（DmgRewrite*）已随四条改写迁唯一光环退役；诅咒门不还原（引擎主干行接棒）。</summary>
        public static Dictionary<string, int> BuildGatePremium()
        {
            var premium = new Dictionary<string, int>
            {
                { "DmgKillsTarget", 2 }, // 消灭目标时
                { "DeclareHit", 2 },     // 宣言结果一致时
                { "DeclareMiss", 2 },    // 宣言落空时（与命中对称）
                { "ProphecyHit", 2 },    // 预言命中时（延迟验证：对手下回合首张出牌结算）
                { "ProphecyMiss", 2 },   // 预言落空时（延迟验证，语义反转）
            };
            // ---- 局面状态族：原子表 Budget 列直通（ComposerCatalog.SituationGates 同源）----
            foreach (var gate in ComposerCatalog.SituationGates)
                premium[gate.Id] = gate.Budget;
            return premium;
        }

        /// <summary>奖励原子的推导费合计（2026-09-14 自 CardEffectConverter 上移——倒计时回合换算与
        /// 合成器【奖励x】预算校验共用同一口径）：单次/单目标锚价——Once、TargetCount=1 的 shim
        ///（防 TargetCount=0 落"全部"档的膨胀——2026-09-13 修复口径固化于此；
        /// 次数档已随 2026-10-30 无上限定案整删）。</summary>
        public static float RewardDerivedCost(List<AtomicEffectInstance> atoms)
            => RewardDerivedCostByColor(atoms).Total;

        /// <summary>RewardDerivedCost 的色费版（2026-10-09 附加诅咒/祝福分支附加费配套）：
        /// 同 shim 口径，返回各色份额（附加费色分配的占比基底）。</summary>
        public static ElementCost RewardDerivedCostByColor(List<AtomicEffectInstance> atoms)
        {
            if (atoms == null || atoms.Count == 0) return new ElementCost();
            var shim = new EffectDefinition { Id = "REWARD_SHIM", Duration = DurationType.Once,
                TargetCount = 1,
                // 时机钉非触发式（2026-10-16 配套；2026-10-30 后仍必要）：裸默认 OnPlay 会吃
                // TimingFactor 的登场系数——奖励按单次面值估值（门槛/换算/附加费/召唤补偿四口共用），
                // 无自身触发时机，不乘时机系数。
                TriggerTiming = TriggerTiming.Activate_Active };
            shim.Effects = atoms;
            return DeriveElementCosts(shim);
        }

        /// <summary>附加诅咒/附加祝福分支附加费（2026-10-09 定案：这两引擎的载荷**不再免费**——
        /// 按分支实际载荷的费用**减半、向上取整**；2026-11-06 载荷效果化：载荷=效果库组合效果引用时
        /// 按引用效果锚价取费，Then 原子列为旧手写数据兼容通道）：
        /// 色分配=载荷推导色费按原色占比分摊减半总额（最大余数法）。
        /// 其余引擎照旧零计价（条件性即折扣——倒计时延迟即付费、拼点差额付门槛）。</summary>
        public static ElementCost EngineBranchSurcharge(BranchPayload payload)
        {
            var result = new ElementCost();
            var full = CursePayloadFullCost(payload);
            float total = full.Total;
            if (total <= 0f) return result;
            int half = (int)Math.Ceiling(total / 2f);
            var colors = full.NonzeroColors().ToList();
            var alloc = new Dictionary<ManaType, int>();
            int assigned = 0;
            foreach (var c in colors)
            {
                int a = (int)Math.Floor(half * full[c] / total);
                alloc[c] = a;
                assigned += a;
            }
            foreach (var c in colors.OrderByDescending(c => half * full[c] / total - alloc[c]))
            {
                if (assigned >= half) break;
                alloc[c]++;
                assigned++;
            }
            foreach (var kv in alloc)
                if (kv.Value > 0) result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>载荷全费（2026-11-06 载荷效果化单源——与运行时 EffectId 优先同口径）：
        /// payload.EffectId 非空按引用效果锚价；否则按 Then 原子列奖励推导费（旧手写数据通道）。
        /// 两通道皆空=0 费。</summary>
        private static ElementCost CursePayloadFullCost(BranchPayload payload)
            => !string.IsNullOrEmpty(payload?.EffectId)
                ? ReferencedEffectCostByColor(payload.EffectId)
                : RewardDerivedCostByColor(payload?.Then);

        // 引用效果解析深度闸（防手写数据自引用循环 A→A→…：资格过滤已拦引擎主干作载荷，
        // 此闸只兜直改 JSON 的环——4 层足够任何合法链）
        [ThreadStatic] private static int _refCostDepth;

        /// <summary>载荷引用效果全费：效果库条目 → ConvertOne → DeriveElementCosts（公开口：
        /// 合成器分支段预览共用同源）。缺失/解析失败=0 费（装载校验已点名，计价不重复告警）。</summary>
        public static ElementCost ReferencedEffectCostByColor(string effectId)
        {
            var result = new ElementCost();
            if (string.IsNullOrEmpty(effectId) || _refCostDepth > 4) return result;
            var data = EffectsLibrary.Resolve(effectId);
            var def = data != null ? CardEffectConverter.ConvertOne(data, effectId) : null;
            if (def == null) return result;
            _refCostDepth++;
            try { return DeriveElementCosts(def, 0); }
            finally { _refCostDepth--; }
        }

        /// <summary>附加诅咒/祝福分支附加费标量（合成器分支段预览文本用）：载荷费 ceil 减半
        ///（Then 原子列口径——效果引用口径走 EngineBranchSurchargeHalfOf）。</summary>
        public static float EngineBranchSurchargeHalf(List<AtomicEffectInstance> then)
            => (float)Math.Ceiling(RewardDerivedCost(then) / 2f);

        /// <summary>附加诅咒/祝福分支附加费标量（2026-11-06 载荷效果化：效果引用/Then 双通道口径——
        /// 合成器分支段预览文本用）。</summary>
        public static float EngineBranchSurchargeHalfOf(BranchPayload payload)
            => (float)Math.Ceiling(CursePayloadFullCost(payload).Total / 2f);

        /// <summary>自平衡门槛（原拼点门槛 2026-10-09 抽公共；同日统一——运行时判定与 UI 短文案同源，
        /// 防显示≠判定）：拼点/死亡计数/元素充盈共用——奖励锚价合计四舍五入（AwayFromZero）、
        /// 下限 1，门槛随 Then 奖励实时变（奖励越贵门槛越高，玩家在合成器 1-5 费区间内调奖励即调门槛）。
        /// 团结（2026-10-20 效果序位换轴）不入此族：x∈[1,6] 手填，门槛=x。</summary>
        public static int RewardThreshold(List<AtomicEffectInstance> then)
            => Math.Max(1, (int)Math.Round(RewardDerivedCost(then), MidpointRounding.AwayFromZero));

        /// <summary>分支奖励全费（2026-11-07 分支奖励效果化——Then 原子列+效果引用双通道合计）：
        /// 引用效果走 <see cref="ReferencedEffectCostByColor"/>（ConvertOne→DeriveElementCosts 同链），
        /// 与运行时先效果后原子的执行序同口径。</summary>
        public static float BranchRewardDerivedCost(List<AtomicEffectInstance> then, string effectId)
            => RewardDerivedCost(then) + ReferencedEffectCostByColor(effectId).Total;

        /// <summary>自平衡门槛·双通道版（2026-11-07 分支奖励效果化）：Then 原子+奖励效果引用合计
        /// 推导——BranchEngines 判定/AtomText 短文案/合成器预算共用，替代裸 Then 版防显示≠判定。</summary>
        public static int RewardThresholdOf(List<AtomicEffectInstance> then, string effectId)
            => Math.Max(1, (int)Math.Round(BranchRewardDerivedCost(then, effectId), MidpointRounding.AwayFromZero));

        /// <summary>倒计时初值回合（与 converter 换算同源）：声明 param&gt;0 用声明值；
        /// 缺省 0=按奖励推导费自动换算（1费=1回合，向上取整下限 1——2026-11-07 效果引用费并入）。</summary>
        public static int CountdownTurnsOf(int param, List<AtomicEffectInstance> then, string effectId = null)
            => param > 0 ? param
            : Math.Max(1, (int)Math.Ceiling(BranchRewardDerivedCost(then, effectId)));

        /// <summary>效果是否携带引擎载荷（settle=Engine——扁平 Effects 与节点化 Steps 双扫）。
        /// 引擎效果头被 EngineHeaderPresetOf 归一为无限档（-1），但主干原子部署单发、奖励由引擎
        /// 自平衡付费——不适用触发限流（2026-10-09 修复；2026-10-30 起仅供时机系数豁免——
        /// 奖励发射由 BranchEngines 每卡每回合 1 次全局规则自限）。</summary>
        internal static bool HasEnginePayload(EffectDefinition def)
        {
            bool Of(AtomicEffectInstance a) => a?.Branch != null && a.Branch.Settle == BranchSettleKind.Engine;
            if (def.Effects != null)
                foreach (var a in def.Effects)
                    if (Of(a)) return true;
            if (def.Steps != null)
                foreach (var s in def.Steps)
                    if (Of(s?.Atomic)) return true;
            return false;
        }

        /// <summary>发动速度计价已退役（2026-10-19 定案：三类卡分类改版把发动速度特化为法术专属
        ///——速度=3−槽数是法术的类特性而非可购溢价，旧规给速 2 效果 ×2 属历史遗留多收；
        /// 其余主动效果速度已全归默认 0。SpeedFactor/SpeedTier 配置位同批整删。）</summary>

        /// <summary>发动时机计价系数（表 Category=TimingFactor 三档）：单次 / 多次（周期·观察族，
        /// 结界专属）/ 生物专属（攻/守/横置族 ×1.5——只有生物可挂）。多次档仍 ×1：**多次生效的成本
        /// 由耐久承担**（结界每次触发 −1 耐久、0.5 蓝/点——耐久即次数池），不按次数加价；生物专属档
        /// 按 ×1.5 计费（宿主自身行动承担次数）。单据 FiresOncePerPaidLifetime / IsTapAnchoredTiming
        /// 归类。引擎载荷豁免：重复性属引擎通道，效果头时机=奖励发射窗，不代表主干发射次数；
        /// 非触发式（主动现付）不乘（恒 1）。</summary>
        public static float TimingCostFactor(EffectDefinition def)
        {
            if (def == null || !def.IsTriggeredEffect) return 1f;
            if (HasEnginePayload(def)) return 1f;
            return TimingCostFactorOf(def.TriggerTiming);
        }

        /// <summary>时机系数单口（计价 TimingCostFactor 与时机下拉档位标签展示同源）：
        /// 表 TimingWeights.json 每时机一行 f = min(E[N], Cutoff)（2026-11-10 时机系数规格 v0.1，
        /// 35 行全枚举覆盖）——<b>Enabled=true（段 2b）才计价读行 Factor</b>；Enabled=false（影子模式，
        /// 默认）与缺行一律回落表 Category=TimingFactor 桶默认：单次 Once / 多次 Repeat（周期·观察族，
        /// 结界专属）/ 生物专属 CreatureOnly（攻/守/横置族 4 值，仅生物可挂）。主动三档行恒 1（现付不进
        /// 时序）、OnTurnStart 行 0（系统专用）。**2026-11-10 资格层新口径：OnAttacked 解绑为双宿主时机
        ///（IsUniversalRepeatTiming）——回落 Repeat 桶 1.0（旧 1.5）、表期读行 Factor=3.0，无生物专属溢价**。
        /// 改 Enabled/表行即同步改计价与展示；切换检查单见验证器 P9-9。</summary>
        public static float TimingCostFactorOf(TriggerTiming timing)
        {
            if (TimingWeights.Enabled && TimingWeights.TryGet(timing, out var row))
                return row.Factor;
            var f = ValueSystemConfigManager.Instance.GetOrCreateConfig().TimingFactorConfig;
            if (EffectEligibility.IsTapAnchoredTiming(timing)) return f.CreatureOnly;
            return FiresOncePerPaidLifetime(timing) ? f.Once : f.Repeat;
        }

        /// <summary>多次档判定（2026-11-05 两档定案配套；合成器宿主过滤/装载校验/验证器共用）：
        /// !FiresOncePerPaidLifetime——回合/阶段/抽牌/伤害/事件观察族 + 攻/守/横置族 + OnSummon。
        /// 注意：攻/守/横置族虽归多次档，但**只有生物可挂**（EffectEligibility.IsTapAnchoredTiming；
        /// 计价另走 CreatureOnly ×1.5，不再按 Repeat ×1）——周期/观察族才是结界专属（耐久即次数池）。</summary>
        public static bool IsRepeatTiming(TriggerTiming t) => !FiresOncePerPaidLifetime(t);

        /// <summary>一发时机分类——**单次档的唯一判定**（2026-10-16 入档；2026-10-30 与计价解耦；
        /// 2026-11-05 两档收敛定案后为单次档的唯一单源）：付费后整个生命期望=1——
        /// 登场/死亡/游戏开始·角色死亡（每局一发）/破坏·除外·离场·墓地回场（付费后基本一次）。
        /// 非单次再分两族：周期/观察族（Repeat，结界专属）与攻/守/横置族（CreatureOnly ×1.5，生物专属，
        /// EffectEligibility.IsTapAnchoredTiming）。
        /// OnSummon 进场**不入档**：OnPlay 超集（token/复活/换主控可不付二次费再触发），有真实二次机会
        /// ——归多次档（系数仍 ×1）。事件观察族（OnOtherCreature* 等）与每回合可复现时机（攻防/横置/
        /// 阶段/抽牌/伤害）期望&gt;1 不入档。消费方：TimingCostFactorOf / IsRepeatTiming /
        /// EffectEligibility（法术合法性+合成器时机排序）。</summary>
        public static bool FiresOncePerPaidLifetime(TriggerTiming timing)
            => timing == TriggerTiming.OnPlay
               || timing == TriggerTiming.OnDeath
               || timing == TriggerTiming.OnGameStart
               || timing == TriggerTiming.OnRoleDeath
               || timing == TriggerTiming.OnDestroy
               || timing == TriggerTiming.OnExile
               || timing == TriggerTiming.OnLeaveBattlefield
               || timing == TriggerTiming.OnReturnFromGraveyard;

        private static float ComputeAtomCost(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, AtomicEffectConfig cfg)
        {
            // 系数链全程不取整（2026-10-12 起）——效果出口逐色取整见 DeriveElementCosts 头注（2026-10-18）。
            float amount = ComputeAtomBaseAmount(atom, def, cfg);
            if (amount <= 0f) return 0f;

            // 错边拆分已删（2026-10-28 P1 定案）：黑白获得经济退役——极性不再参与计价拆分，
            // 有害原子锁己方域照常全价计费（自缚设计的代价语义由代价栏抵扣承担）。

            // 目标数量计价（2026-10-05 档位化，表 PricingTier：1:1 / 2:1.5 / 3:2 / 全部:3；
            // 2026-10-09 每原子下沉——按该原子有效数量档计）；
            // 全部/任意语义无法在构建期确定——按"全部"档计（原期望 4 口径退役）。
            // 固有全域原子（类型伤害/全体治疗）范围溢价已含 BaseCost，数量恒 ×1。
            // 无目标域（2026-10-03 规则光环配套）：数量是目标数量语义，无目标可乘——恒 ×1
            //（否则规则光环按表锚价虚高"全部"档倍数）。
            float n = QuantityFactor(atom, def);
            if ((domain == null || domain.Count == 0) && n > 1f)
                n = 1f;
            if (n > 1f)
                amount *= n;

            // 发动时机计价（表 TimingFactor 三档：单次/多次 ×1 + 生物专属攻/守/横置 ×1.5；
            // 多次生效的成本由结界耐久承担；引擎载荷/非触发式恒 ×1 在 TimingCostFactor 内判）。
            // 系数判 ≠1 即乘（三档调离 1 时自动生效）。
            float timingFactor = TimingCostFactor(def);
            if (Math.Abs(timingFactor - 1f) > 0.001f)
                amount *= timingFactor;

            // 发动速度计价已退役（2026-10-19：速度特化法术专属=类特性非溢价——旧 ×1.5/×2 档随拆）。

            // 双方同时作用减半（2026-10-03 用户定案）：效果同时作用于双方——
            // 双侧域 + 全取档/显式多目标（双方全体恢复/双方各消灭一生物）——计费 ×0.5（对称面让利）；
            // 规则光环不在此列（2026-10-29 去对称让利：表价即实价，无论作用范围）；双侧域单体任选一侧不減。
            if (IsSymmetricBothSides(atom, def, domain))
                amount *= SymmetricDiscountFactor;

            return amount;
        }

        /// <summary>双方同时作用减半系数（2026-10-03 用户定案；2026-10-05 迁表 CardCost.SymmetricDiscountFactor）。</summary>
        public static float SymmetricDiscountFactor => ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig.SymmetricDiscountFactor;

        /// <summary>
        /// 是否「同时作用双方」（2026-10-03 用户定案减半口径；2026-10-09 逐原子目标制修订；
        /// 2026-10-29 去对称让利——规则光环臂整删，表价即实价）：
        /// - 双侧域（SideLock=0 且域非空）且 全取档（SelectionModeRules.IsTakeAll）——域内全收
        ///   必然覆盖双方，让利成立；
        /// - 旧共享目标数据（PerAtomTargets=false，header 声明作用范围）保留显式多目标减半臂
        ///   （有效数量≥2——「双方各一」语义）。
        /// 规则光环（ModifyGameRule·无目标域）不再参与（2026-10-29 定案：对称仪典表价直接落
        /// 实价 3/6 档——对称面让利在表侧兑现，计价侧恒全价）；无目标域的其余原子恒 false。
        /// 逐原子目标制（PerAtomTargets=true，2026-10-09）数量 1/2/3=弹窗任选 N 个——是否跨侧
        /// 运行时才知，**不再按数量≥2 判对称减半**（旧口径曾令双侧域伤害 1/2/3 档经 ×1.5/×2 再 ×0.5
        /// 后费用全等——目标数量计价失效根因）。
        /// 单侧锁（可按最优边全价）与双侧域单体任选一侧均不減。
        /// </summary>
        public static bool IsSymmetricBothSides(AtomicEffectInstance atom, EffectDefinition def, List<int> domain)
        {
            if (domain == null || domain.Count == 0) return false;
            if (SideLock(domain) != 0) return false;
            if (SelectionModeRules.IsTakeAll(def != null ? def.SelectionMode : 0)) return true;
            return def != null && !def.PerAtomTargets
                   && (atom != null ? atom.EffectiveTargetCount(def) : def.TargetCount) >= 2;
        }

        /// <summary>遗忘行（GrantTemporary·极性−1·白2）——衍生物召唤「负面指示物补偿」的表驱动真源
        ///（2026-10-12 裁决：补偿=该行**单层基准费用**/只，原写死 −1灰 退役；后续遗忘改价自动跟随；
        /// 行缺失=null → 补偿 0）。计价与公式展开（ComputeAtomBaseAmount / BaseExprOf）共用此口。</summary>
        private static Attribute.AtomicEffectConfig ForgetRow => AtomicEffectTable.GetByType(AtomicEffectType.GrantTemporary);

        /// <summary>召唤负面补偿的**按色落位**（2026-10-12 裁决）：补偿锚（遗忘行 ManaList·现行白2）
        /// 逐色**同色优先扣模板同色分量**——白色锚优先补偿白色，模板无该色（或不足）的溢出量
        /// 自动转为**扣灰色**（灰亦不足按可得计，各桶下限 0）。返回补偿后的模板逐色桶 +
        /// 实际落位文案（「2白」「2灰」「1白1灰」）。计价标量（ComputeAtomBaseAmount）/
        /// 分色（ComputeAtomCostByColor）/公式展开（BaseExprOf）三口共用，防漂移。</summary>
        private static Dictionary<ManaType, float> SummonCompensatedBuckets(CardData template, out string compZh)
        {
            var buckets = new Dictionary<ManaType, float>();
            if (template != null)
                foreach (var kv in CardCostService.ContentPriceByColor(template))
                    buckets[kv.Key] = kv.Value;

            var applied = new Dictionary<ManaType, float>();
            float overflow = 0f;
            var anchor = ForgetRow?.ManaList;
            if (anchor != null)
            {
                foreach (var color in anchor.NonzeroColors())
                {
                    if (color == ManaType.Gray) continue; // 灰锚分量并入溢出——灰=兜底色最后扣
                    buckets.TryGetValue(color, out float have);
                    float part = Math.Min(anchor[color], have);
                    if (part > 0f) { buckets[color] = have - part; applied[color] = part; }
                    overflow += anchor[color] - part;
                }
            }
            buckets.TryGetValue(ManaType.Gray, out float gray);
            float grayPart = Math.Min(overflow, gray);
            if (grayPart > 0f) { buckets[ManaType.Gray] = gray - grayPart; applied[ManaType.Gray] = grayPart; }

            var parts = new List<string>();
            foreach (var kv in applied)
                if (kv.Value > 0f) parts.Add($"{kv.Value:0.##}{ColorZh(kv.Key)}");
            compZh = parts.Count == 0 ? "0" : string.Join("", parts);
            return buckets;
        }

        /// <summary>
        /// 原子基础量（数量档/抽牌缺陷之前的单价）：检索按筛选维度档，其余通用公式。
        /// 计费（ComputeAtomCost）与代价全价（PayloadUnitGrant）共用，保证同源不漂移。
        /// 2026-10-12 起本口与 ComputeAtomCost 全程不取整（内部真值带小数，如 回复生命 锚 0.5绿/点）
        /// ——出口取整见 DeriveElementCosts 头注（2026-10-18 效果合成阶段逐色取整）。
        /// </summary>
        private static float ComputeAtomBaseAmount(AtomicEffectInstance atom, EffectDefinition def, AtomicEffectConfig cfg)
        {
            // 检索：按筛选维度档计费，替代通用公式。维度档存于 atom.StringValue（默认单一维度）。
            if (atom.Type == AtomicEffectType.SearchDeck)
                return FilterPrecisionCost(atom);

            // 召唤衍生物（2026-10-31 动态费用改版，逆转 2026-10-07 表价清零）：总价 = 表行锚价（效果
            // 本体费·表行 MountKinds 已标「动态费用」）＋模板段×数量。本体锚价恒收——未绑引用卡/
            // 模板不可解析时只剩本体费（卡组合界面绑定引用卡 ID 后才见完整费，构筑期校验拦未绑）；
            // 模板零费桶同样只剩本体费。数量/持续档不乘（量级即最终口径），触发档仍由外层统一乘。
            // 内容全价（2026-10-10 无底盘定案，取代同日早间「声明费+ChassisAdjust 回加」案）：
            // 衍生物没走「入组 1+抽牌 2」，吃不到 3 点底盘——单价 = 模板内容全价
            //（S含耐久+K+E，不乘 d(C)）+ 结构费全额（攻/守/槽×1，无预算抵扣）= ContentPriceOf。
            // 负面补偿（2026-10-12 裁定表驱动化＋按色落位）：召唤自带一层遗忘指示物（表行 DisplayName
            // 同口径），单价再 − 遗忘行（GrantTemporary）**单层基准费用**/只——白色锚优先补偿模板白色
            // 分量、无白自动转扣灰色（落位单源 SummonCompensatedBuckets，各桶下限 0）。原写死 −1 灰
            // 退役，后续遗忘行改价补偿自动跟随（行缺失=0 不补偿）。计费与错边黑白获得共用本口（同源不漂移）。
            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var resolver = Attribute.Handlers.SummonTokenHandler.ResolveTemplate; // 引用卡ID 唯一解析器
                var template = string.IsNullOrEmpty(atom.StringValue) ? null : resolver?.Invoke(atom.StringValue);
                float anchor = cfg != null ? cfg.TotalUnitCost : 0f; // 本体费=表行锚价（恒收）
                if (template == null) return anchor;
                float unit = 0f;
                foreach (var kv in SummonCompensatedBuckets(template, out _))
                    unit += kv.Value;
                if (unit <= 0f) return anchor;
                int count = Math.Max(1, atom.Value);
                return anchor + unit * count;
            }

            // 属性价梯（2026-09-13 定案：攻/血同锚 0.5/+1，按持续档定价——取代通用公式与持续折扣）：
            // 修改族（ModifyPower/ModifyLife）档价：固定1回合（UntilEndOfTurn/UntilNextTurn）0.5
            //（2026-09-16 统一档：限时指示物两档合一=持有者回合结束，费用一律按 1 回合计）、
            // 换区移除（UntilLeaveBattlefield/WhileCondition）1.5、换区不移除（Permanent=指示物永久档）2.0；
            // 改写族（SetPower/SetLife=设置直改，视同本体）3.0。
            float statTier = StatTierPrice(atom.Type, def);
            if (statTier > 0f)
                return statTier * Math.Abs(atom.Value);

            // 指示物统一计价（2026-10-08 层改造定案；2026-10-31 指示物定案收窄）：**基础费用 × 层数**——
            // 表行已全量去 {value}（每次发动恒加 1 层），恒按 1 层价=表行 ManaList 锚计
            //（max(1,|value|) 钳仅服务存量多层数据的旧卡显示，不产生新多层）。
            // 判定=表行 Tags 行为三型（衰退型/换区清除型/生效自减型，与 CounterSpec.Class 同源）。
            if (IsCounterAtom(cfg))
                return cfg.TotalUnitCost * Math.Max(1, Math.Abs(atom.Value));

            // 控制权三档（2026-09-13 定案）：回合级临时（UET/UNT/ForTurns≤2）×1.2 /
            // 持续到离场（ULB/WhileCondition）×1.6 / 改写持有者（Permanent——控制+owner 换写，
            // 弹回洗回死亡都归新主）×3.0。ChangeOwner 显式原子恒 ×3.0（同第三档）。
            if (atom.Type == AtomicEffectType.GainControl || atom.Type == AtomicEffectType.ChangeOwner)
            {
                float ctrlMult = def.Duration == DurationType.Permanent || atom.Type == AtomicEffectType.ChangeOwner ? 3f
                    : (def.Duration == DurationType.UntilLeaveBattlefield || def.Duration == DurationType.WhileCondition) ? 1.6f
                    : 1.2f;
                return cfg.TotalUnitCost * ctrlMult;
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
                return cfg.TotalUnitCost * grantMult;
            }

            if (cfg == null || cfg.TotalUnitCost <= 0f)
                return 0f;

            // 规则光环（2026-10-07 范围化；2026-10-31 阶段D：判定改行类型——Aura* 新型 ∪
            // 未翻新旧共享型 ModifyGameRule，单源=RuleAuraComponents.IsAuraLike）：Value=作用范围序
            //（RuleAuraScope）非量级——不乘 magnitude，
            // 恒单价×成本乘数×持续档（与旧口径 value=1 等价）；不吃对称让利（2026-10-29 去对称让利——
            // 表价即实价；含负面光环——2026-10-07 改写降级回滚，改写族箭头计价口径退役；舍身（2026-10-30 改版战斗法师）2026-10-16 关键词化出族）。
            // 2026-10-30 单侧溢价（2026-11-06 定案修订：±1 行缺省双方=表价档，卡面可收窄极性单侧
            //——+1 仅己方/−1 仅对方）：表价按对称双方校准 → 单侧档 ×RuleAuraSingleSideFactor
            //（CardCostConfig，默认 1.5——具体值用户调）；极性 0 行恒双方（converter 已矫正 value）×1。
            if (RuleAuraComponents.IsAuraLike(atom.Type))
            {
                float ruleMult = cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f;
                var ruleAttr = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
                var scope = (RuleAuraScope)atom.Value;
                float sideFactor = scope == RuleAuraScope.Own || scope == RuleAuraScope.Opponent
                    ? ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig.RuleAuraSingleSideFactor
                    : 1f;
                return cfg.TotalUnitCost * ruleMult * sideFactor
                       * ruleAttr.GetDurationDiscount(def.Duration)
                       / ruleAttr.GetDurationDiscount(DurationType.Once);
            }

            // CostMultiplier 在表加载时默认 1.0；目标范围等可在配置中放大费用。
            float multiplier = cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f;
            int magnitude = Math.Max(1, atom.Value);

            var attrCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
            float durationFactor = attrCfg.GetDurationDiscount(def.Duration)
                                   / attrCfg.GetDurationDiscount(DurationType.Once);

            return cfg.TotalUnitCost * multiplier * magnitude * durationFactor;
        }

        /// <summary>
        /// 分色计价（2026-09-14 ManaList 定案；2026-10-04 位置数组化）：**先按既有标量管线算总价**
        /// （ComputeAtomCost——含梯价/错边拆分/数量期望/触发连乘/缺陷减费/落区全部规则，一字不改），
        /// 再按表行费用构成份额**比例拆分**到各色（序数序）。单色行=行为与旧口径完全一致；
        /// 混合色行=总价不变、构成按份额分布（余数归序数序末个非零色保总额）。
        /// 例外：召唤衍生物（2026-10-31 动态费用改版）两段落色——本体段按表行锚价份额、
        /// 模板段按补偿后逐色桶（2026-10-07 表价清零口径已随锚价复收逆转）。
        /// </summary>
        public static Dictionary<ManaType, float> ComputeAtomCostByColor(AtomicEffectInstance atom, EffectDefinition def,
            List<int> domain, AtomicEffectConfig cfg)
        {
            // 总价与分色份额内部均不取整（float 比例拆分天然保额）——
            // 出口取整在 DeriveElementCosts（2026-10-18 效果合成阶段逐色四舍五入）。
            var result = new Dictionary<ManaType, float>();
            float total = ComputeAtomCost(atom, def, domain, cfg);
            var cost = cfg?.ManaList;
            // 召唤衍生物（2026-10-31 动态费用改版）：分色不走通用比例分摊——本体段（表行锚价）
            // 按行 ManaList 份额、模板段照旧**补偿后逐色桶**直出（落位单源 SummonCompensatedBuckets）；
            // 外层系数（触发等）按未缩放构成占比等比缩放两段（总额与标量口径一致）。
            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var resolver = Attribute.Handlers.SummonTokenHandler.ResolveTemplate; // 引用卡ID 唯一解析器
                var template = string.IsNullOrEmpty(atom.StringValue) ? null : resolver?.Invoke(atom.StringValue);
                if (template != null)
                {
                    var unitBuckets = SummonCompensatedBuckets(template, out _);
                    float bucketSum = 0f;
                    foreach (var kv in unitBuckets) bucketSum += kv.Value;
                    if (bucketSum > 0f && total > 0f)
                    {
                        float anchor = cfg != null ? cfg.TotalUnitCost : 0f;
                        int count = Math.Max(1, atom.Value);
                        float raw = anchor + bucketSum * count; // 未缩放构成（外层系数前）
                        float anchorPart = raw > 0f ? total * (anchor / raw) : 0f;
                        float anchorSum = cost != null ? cost.Total : 0f;
                        if (anchorPart > 0f && anchorSum > 0f)
                            foreach (var color in cost.NonzeroColors())
                            {
                                result.TryGetValue(color, out var prev);
                                result[color] = prev + anchorPart * (cost[color] / anchorSum);
                            }
                        // 模板段逐桶缩放（两段合计保总额==total）
                        float scale = (total - anchorPart) / bucketSum;
                        foreach (var kv in unitBuckets)
                        {
                            if (kv.Value <= 0f) continue;
                            result.TryGetValue(kv.Key, out var prev);
                            result[kv.Key] = prev + kv.Value * scale;
                        }
                        return result;
                    }
                }
                // 模板不可解析/零费桶：只剩本体锚价段——落通用路径按行 ManaList 份额拆（同口径）
            }
            if (total <= 0f || cost == null || cost.IsZero) return result;

            float unitSum = cost.Total;
            if (unitSum <= 0f)
            {
                result[cost.PrimaryColor] = total; // 份额退化（不应发生）——全额落主色
                return result;
            }

            foreach (var color in cost.NonzeroColors())
            {
                float share = total * (cost[color] / unitSum);
                result.TryGetValue(color, out var prev);
                result[color] = prev + share;
            }
            return result;
        }

        // ======================================== 费用预览公式展开（2026-10-10 合成器「参与变量写全」） ========================================

        /// <summary>计价访问域（费用预览公式展开用）：per-mode 域优先回落主序列域、逐原子目标制
        /// 换该原子自身域——与 DeriveElementCosts 内部同口径，防公式与数值两口各算各的。</summary>
        public static List<int> PricingVisitDomain(EffectDefinition effect, AtomicEffectInstance atom, int modeIndex = 0)
            => PricingDomain(effect, atom, ResolveVisitDomain(effect, modeIndex));

        /// <summary>单原子计价公式展开（效果合成器顶栏费用预览——把参与变量写全，如
        /// 「基准1红×5×数量档1.5×时机档2」）：数值真源仍是 ComputeAtomCost，本口只展开显示构成——
        /// 基准量＋逐段系数链（系数=1 不列），分档判定复用同一批 primitives
        ///（BaseExprOf 各分档 / QuantityFactor / TimingCostFactor /
        /// IsSymmetricBothSides），不另立计价口径。系数顺序与 ComputeAtomCost
        /// 管线一致：基准内系（量级/表乘数/持续/控制/赋予档）→数量档→时机档→双方减半
        ///（触发档已随 2026-10-30 无上限定案删除；错边拆分已随黑白获得经济删除，2026-10-28 P1）。
        /// 基准内的乘法在数值侧折叠为一次 round（ComputeAtomBaseAmount），展开链只作显示——
        /// 末端结果由调用方取 DeriveElementCosts 实值，不重算。</summary>
        public static (string baseExpr, List<string> factorExprs) AtomCostFormula(
            AtomicEffectInstance atom, EffectDefinition def, List<int> domain)
        {
            var factors = new List<string>();
            if (atom == null) return ("基准0", factors);

            string baseExpr = BaseExprOf(atom, def, ResolvePricingConfig(atom), factors);

            // 管线系数（ComputeAtomCost 同序同条件；速度档已随速度计价退役 2026-10-19；
            // 错边拆分已随黑白获得经济删除 2026-10-28 P1）
            float n = QuantityFactor(atom, def);
            if ((domain == null || domain.Count == 0) && n > 1f) n = 1f; // 无目标域不乘数量（同 ComputeAtomCost）
            AddFactor(factors, "数量档", n);
            bool creatureOnly = def != null && EffectEligibility.IsTapAnchoredTiming(def.TriggerTiming);
            AddFactor(factors, creatureOnly ? "生物专属档" : "时机档", TimingCostFactor(def));
            if (IsSymmetricBothSides(atom, def, domain))
                AddFactor(factors, "双方减半", SymmetricDiscountFactor);
            return (baseExpr, factors);
        }

        /// <summary>系数入链（数值=1 不列；label 空=裸量级乘数，如 ×5）。</summary>
        private static void AddFactor(List<string> factors, string label, float v)
        {
            if (Math.Abs(v - 1f) > 0.001f)
                factors.Add(string.IsNullOrEmpty(label) ? $"{v:0.##}" : $"{label}{v:0.##}");
        }

        /// <summary>基准量文案（ComputeAtomBaseAmount 各分档的显示面）：基准内乘数（量级/表乘数/
        /// 持续/控制/赋予档）以系数续在基准后——取值与数值侧同一表达式，防两口漂移。</summary>
        private static string BaseExprOf(AtomicEffectInstance atom, EffectDefinition def,
            AtomicEffectConfig cfg, List<string> factors)
        {
            if (atom.Type == AtomicEffectType.SearchDeck)
                return $"检索精度{FilterPrecisionCost(atom)}";

            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var resolver = Attribute.Handlers.SummonTokenHandler.ResolveTemplate; // 引用卡ID 唯一解析器
                var template = string.IsNullOrEmpty(atom.StringValue) ? null : resolver?.Invoke(atom.StringValue);
                // 2026-10-31 动态费用改版句式：基准【表行锚价】（效果本体·恒收）＋模板段按个计
                //（2026-10-12 句式：单价构成入【】——（模板【x】−负面指示物补偿【1】）×召唤数量【x】，
                // 数量只乘模板段不乘本体基准，故并入基准句不另列系数）。模板解析不到时明示「计0」、
                // 本体费照列（卡组合界面绑定引用卡后才见完整费）。
                float anchorTotal = cfg != null ? cfg.TotalUnitCost : 0f;
                string anchorZh = anchorTotal > 0f ? $"基准{AnchorZh(cfg.ManaList)}＋" : "";
                if (template == null)
                    return $"{anchorZh}（模板【未解析·计0】）"; // 数值在 ComputeAtomBaseAmount（模板段=0）
                var content = new ElementCost();
                foreach (var kv in CardCostService.ContentPriceByColor(template))
                    if (kv.Value != 0) content[kv.Key] = kv.Value;
                SummonCompensatedBuckets(template, out string compZh); // 落位单源：白优先白、无白转灰（显示实际落位色）
                return $"{anchorZh}（模板【{AnchorZh(content)}】−负面补偿【{compZh}】）×召唤数量【{Math.Max(1, atom.Value)}】"; // −遗忘行单层锚价/只（按色落位，数值在 ComputeAtomBaseAmount）
            }

            float statTier = StatTierPrice(atom.Type, def);
            if (statTier > 0f)
            {
                AddFactor(factors, "", Math.Abs(atom.Value));
                return $"属性档{statTier:0.##}/点";
            }

            if (IsCounterAtom(cfg))
            {
                AddFactor(factors, "", Math.Max(1, Math.Abs(atom.Value)));
                return $"基准{AnchorZh(cfg?.ManaList)}";
            }

            if (atom.Type == AtomicEffectType.GainControl || atom.Type == AtomicEffectType.ChangeOwner)
            {
                float ctrlMult = def.Duration == DurationType.Permanent || atom.Type == AtomicEffectType.ChangeOwner ? 3f
                    : (def.Duration == DurationType.UntilLeaveBattlefield || def.Duration == DurationType.WhileCondition) ? 1.6f : 1.2f;
                AddFactor(factors, "控制档", ctrlMult);
                return $"基准{AnchorZh(cfg?.ManaList)}";
            }

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
                    default: grantMult = 2.0f; break;
                }
                AddFactor(factors, "持续档", grantMult);
                return $"基准{AnchorZh(cfg?.ManaList)}";
            }

            if (cfg == null || cfg.TotalUnitCost <= 0f)
                return "基准0";

            var attrCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
            float durationFactor = attrCfg.GetDurationDiscount(def.Duration)
                                   / attrCfg.GetDurationDiscount(DurationType.Once);
            if (RuleAuraComponents.IsAuraLike(atom.Type)) // 阶段D：行类型判定（Aura* ∪ 未翻新 ModifyGameRule）
            {
                AddFactor(factors, "表乘数", cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f);
                // 单侧溢价（2026-10-30）：与 ComputeAtomBaseAmount 同源——±1 行单侧档 ×系数，双方档 ×1
                var scope = (RuleAuraScope)atom.Value;
                if (scope == RuleAuraScope.Own || scope == RuleAuraScope.Opponent)
                    AddFactor(factors, "单侧",
                        ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig.RuleAuraSingleSideFactor);
                AddFactor(factors, "持续档", durationFactor);
                return $"基准{AnchorZh(cfg.ManaList)}";
            }

            AddFactor(factors, "表乘数", cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f);
            AddFactor(factors, "", Math.Max(1, atom.Value));
            AddFactor(factors, "持续档", durationFactor);
            return $"基准{AnchorZh(cfg.ManaList)}";
        }

        /// <summary>锚价构成文案（序数序拼「N色」——表行 ManaList / 模板内容价共用）。</summary>
        private static string AnchorZh(ElementCost cost)
        {
            // 2026-10-12 小数直通：锚价按真值显示（如 回复生命 0.6绿——整数不带小数位）
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var c in cost.NonzeroColors())
                parts.Add($"{cost[c]:0.##}{ColorZh(c)}");
            return string.Join("", parts);
        }

        private static string ColorZh(ManaType m) => m switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.Gray => "灰",
            _ => m.ToString(),
        };

        // ======================================== 代价栏有效域（2026-10-28 P1b：自伤 + 资敌双臂） ========================================

        /// <summary>代价栏有效域计算（2026-10-28 P1b 定案：代价收 p≠0 且锁在**受益方反面**的原子）。
        /// p&lt;0（有害）→ 锁己方（自伤）；p&gt;0（有益）→ 锁对方（资敌：给对手回血/增益/防护/送牌）；
        /// p=0（中性）不可作代价（null,false）。
        /// 2026-11-03 双侧域定案：只收表行 TargetKinds **同时含己方与对方条目**的双侧行——
        /// 只能作用于单方面的行（SideLock=±1 纯己方/纯对方）不可作代价（镜像改写单侧行语义的
        /// 放行口就此关闭；空域行同判不可入）；双侧行收窄到锁定侧成员（免写=null 表示表默认域同效）。
        /// 返回 (kinds, eligible)：kinds=null 表示免写（表默认域同效）；合成器放置口与候选过滤共用本口。</summary>
        public static (List<int> kinds, bool eligible) PayloadCostDomain(AtomicEffectConfig cfg)
            => PayloadCostDomain(cfg, cfg?.GetTargetKindList());

        /// <summary>组装效果代价版（2026-10-16）：域判定按条目**有效域**（组合期已解析的 entry.kinds，null 回落表默认）。
        /// 免写(null)=有效域本就整侧匹配——调用方保持条目原 kinds 不动。</summary>
        public static (List<int> kinds, bool eligible) PayloadCostDomain(AtomicEffectConfig cfg, List<int> effectiveKinds)
        {
            if (cfg == null) return (null, false);
            var kinds = effectiveKinds;
            float p = Math.Clamp(cfg.Polarity, -1f, 1f);
            if (p == 0f) return (null, false); // p=0 不可作代价（2026-10-28 P1b）

            // ③ 双侧域闸（2026-11-03 定案）：单侧行（纯己方/纯对方，SideLock=±1）与空域行不可作代价——
            // 代价只收同时含己方与对方条目的双侧行，锁侧由下方收窄段统一收窄（单侧行镜像放行口关闭）。
            var rowKinds = cfg.GetTargetKindList();
            if (rowKinds.Count == 0 || SideLock(rowKinds) != 0)
                return (null, false);

            bool wantEnemy = p > 0f; // 锁定侧 = 受益方反面：p<0 锁己方 / p>0 锁对方（资敌臂恢复）
            var side = kinds.Where(k => TargetKindRules.IsEnemySide(k) == wantEnemy).ToList();
            if (side.Count > 0)
                return side.Count == kinds.Count ? (null, true) : (side, true);

            var mirrored = TargetKindRules.MirrorDomain(kinds); // 纯反面侧 → 逆转到锁定侧（防御分支：双侧行闸后常规输入不可达）
            return SideLock(mirrored) == (wantEnemy ? 1 : -1) ? (mirrored, true) : (null, false);
        }

        /// <summary>代价契约要求的侧锁（2026-10-28 P1b）：p&lt;0 → −1（锁己方·自伤）；
        /// p&gt;0 → +1（锁对方·资敌）；p=0 → 0（不可作代价）。与 <see cref="SideLock"/> 同尺，
        /// 转换器契约校验（ConvertOne / ConvertPayloadForDisplay）与验证器共用。</summary>
        public static int PayloadCostRequiredSideLock(float polarity)
        {
            float p = Math.Clamp(polarity, -1f, 1f);
            return p < 0f ? -1 : p > 0f ? 1 : 0;
        }

        /// <summary>
        /// Payload 效果型代价的全价（单目标，未封顶）：按 Once/单目标/战场落区的合成组合层计价
        ///（2026-10-28 P1：黑白获得经济退役——本值不再用于补偿发放，只作代价抵扣（SumPayloadPrice）
        /// 与构筑期计价锚；极性乘子删除，全价直出）。
        /// </summary>
        public static int PayloadUnitGrant(AtomicEffectInstance atom)
        {
            if (atom == null) return 0;
            var cfg = ResolvePricingConfig(atom); // 行身份（2026-10-03）：变体行按 RowHashId 取锚
            return (int)Math.Round(ComputeAtomBaseAmount(atom, PayloadGrantDef, cfg), MidpointRounding.AwayFromZero); // 小数直通：末尾一次约等于
        }

        /// <summary>Payload 计价合成组合层（Duration=Once、TargetCount=1）。</summary>
        private static readonly EffectDefinition PayloadGrantDef = new EffectDefinition
        {
            Id = "COST_PAYLOAD",
            Duration = DurationType.Once,
            TargetCount = 1,
        };

        /// <summary>构筑期 grant 累计（DeriveElementGrants 的访问器）已删（2026-10-28 P1：黑白获得经济退役）。</summary>

        private static int FilterPrecisionCost(AtomicEffectInstance atom)
        {
            // 检索改宣言卡名（2026-09-03 定案）：StringValue = 宣言的卡名（构筑期预置），
            // 精度恒为按名称检索（ExactCard 单档=3）；未预置宣言名视为最低档 1。
            // 2026-10-22 定案：BranchConfig 目录层已删（配置只剩原子表 + ValueSystemConfig 两张），
            // ExactCard=3 收敛为代码常量。
            if (string.IsNullOrEmpty(atom.StringValue)) return 1;
            return 3;
        }

        private static void AccumulateSubEffects(AtomicEffectInstance atom, EffectDefinition def, List<int> domain, Dictionary<ManaType, float> byColor)
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

        // ======================================== 代价型原子极性裁定（2026-11-02 定案） ========================================

        /// <summary>代价型原子表（2026-11-02 用户定案）：效果栏里「指向自己」时只可能是代价、
        /// 无收益面的原子类型 → 构成「指向自己即代价」的 TargetKind 白名单。
        /// 命中（原子有效指向纯己方侧且含白名单位）= 效果栏计 0 费 + <see cref="SelfCostViolations"/>
        /// 上报；代价语义应改走代价栏（payloads/Costs 行·色锁抵扣）或把 kinds 改指对侧。
        /// 至少覆盖用户点名四类：弃牌@己方手牌 / 造成伤害@Self（自伤）/ 消灭己方 / 磨己方牌库；
        /// 同族补：摧毁己方无生命单位、生命流失@己方、永久属性减少@己方。
        /// **不得按表行 Polarity&lt;0 泛化**——Polarity 只说明「对目标有害」，不说明目标是谁。
        /// （历史例已随 2026-11-08 目标选择重构结构性消除：迸发曾「行域指己方参照而伤害落对方」，
        /// 泛化会误杀；现参照域独立走 RefDomain 列、Domain 恒为受体域——两域分离后该形态不可再产生。
        /// 白名单机制保留：防未来「有害原子行域纯己方」的同类误定价——注意参照族行 Domain 指受体
        ///（对方侧），天然不触本闸。）</summary>
        private static readonly Dictionary<AtomicEffectType, HashSet<int>> SelfCostAtoms = new Dictionary<AtomicEffectType, HashSet<int>>
        {
            { AtomicEffectType.DiscardCard, new HashSet<int> { (int)TargetKind.OwnHand } },          // 弃牌@己方手牌
            { AtomicEffectType.DealDamage, new HashSet<int> { (int)TargetKind.Self, (int)TargetKind.OwnLivingUnit } }, // 自伤（角色=Self/己方单位）
            { AtomicEffectType.Devour, new HashSet<int> { (int)TargetKind.OwnLivingUnit } },         // 消灭己方生物
            { AtomicEffectType.Smash, new HashSet<int> { (int)TargetKind.OwnNonLivingUnit } },       // 摧毁己方地/结界
            { AtomicEffectType.MillCard, new HashSet<int> { (int)TargetKind.OwnDeck } },             // 磨己方牌库
            { AtomicEffectType.LifeLoss, new HashSet<int> { (int)TargetKind.Self, (int)TargetKind.OwnLivingUnit } }, // 己方生命流失
            { AtomicEffectType.AddPermanentPowerDown, new HashSet<int> { (int)TargetKind.OwnLivingUnit } }, // 永久攻减@己方（原永久属性减少拆轴承袭）
            { AtomicEffectType.AddPermanentLifeDown, new HashSet<int> { (int)TargetKind.OwnLivingUnit } },  // 永久血减@己方（同上）
        };

        /// <summary>是否「代价型原子指向自己」：类型在 <see cref="SelfCostAtoms"/> 白名单、
        /// 有效指向（原子自身 kinds 优先，回落效果级共享域）纯己方侧、且含该类型的白名单位。
        /// 含对侧成员=可改打对手（非纯代价），不命中。</summary>
        public static bool IsSelfDirectedCostAtom(AtomicEffectInstance atom, EffectDefinition def = null)
        {
            if (atom == null) return false;
            if (!SelfCostAtoms.TryGetValue(atom.Type, out var costKinds)) return false;
            var kinds = atom.TargetKinds != null && atom.TargetKinds.Count > 0
                ? atom.TargetKinds
                : def?.TargetDomain;
            if (kinds == null || kinds.Count == 0) return false;
            if (SideLock(kinds) != -1) return false; // 纯己方侧才成立
            return kinds.Any(costKinds.Contains);
        }

        /// <summary>极性校验（2026-11-02）：扫整卡全部计费原子，报出「代价型原子指向自己」组合——
        /// 每条含效果名 + 原子行 + 指向 + 表锚价（未按极性裁定时的全价口径，供人工核对意图）。
        /// 命中不是计价错误（计价层已按 0 费裁定），是设计意图问题：自伤/弃己牌按代价设计
        /// 应走代价栏（payloads），打击对手应改指对侧。消费方：重推工具 RegenOneDeck（逐卡
        /// LogError）与端到端验证（Assert 0 命中）。抉择全模式、分支 Then 奖励、子效果全覆盖。</summary>
        public static List<string> SelfCostViolations(CardData card)
        {
            var result = new List<string>();
            if (card?.Effects == null || card.Effects.Count == 0) return result;
            foreach (var def in CardEffectConverter.ConvertAll(card.Effects, card.ID))
            {
                if (def == null) continue;
                CheckSelfCostSteps(def, def.Steps, result);
                foreach (var atom in def.Effects) // 扁平形态（未配 Steps 的旧卡）
                    CheckSelfCostAtom(def, atom, result);
            }
            return result;
        }

        private static void CheckSelfCostSteps(EffectDefinition def, List<RuntimeEffectStep> steps, List<string> result)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.Kind == RuntimeStepKind.Atomic) CheckSelfCostAtom(def, step.Atomic, result);
                else if (step.Kind == RuntimeStepKind.Choice && step.Choices != null)
                    foreach (var mode in step.Choices) // 抉择全模式巡检（计价只看所选模式，校验面更宽）
                        CheckSelfCostSteps(def, mode, result);
            }
        }

        private static void CheckSelfCostAtom(EffectDefinition def, AtomicEffectInstance atom, List<string> result)
        {
            if (atom == null) return;
            if (IsSelfDirectedCostAtom(atom, def))
            {
                var cfg = ResolvePricingConfig(atom);
                var kinds = atom.TargetKinds != null && atom.TargetKinds.Count > 0 ? atom.TargetKinds : def?.TargetDomain;
                string kindsZh = kinds == null ? "?" : string.Join(",", kinds.Select(k => TargetKindRules.ZhNameOf((TargetKind)k)));
                string anchor = cfg?.ManaList != null ? AnchorZh(cfg.ManaList) : "?";
                result.Add($"效果[{def.DisplayName ?? def.Id}] 原子[{cfg?.DisplayName ?? atom.Type.ToString()}] 指向[{kindsZh}]" +
                           $"（代价型·指向自己·表锚价{anchor}）——效果栏已按极性裁定计 0 费；" +
                           $"按代价设计请改走代价栏(payloads)，打击对手请改指对侧");
            }
            foreach (var sub in atom.SubEffects) CheckSelfCostAtom(def, sub, result);       // 元效果子原子
            if (atom.Branch?.Then != null) CheckSelfCostThen(def, atom.Branch.Then, result); // 分支奖励原子同口径
        }

        private static void CheckSelfCostThen(EffectDefinition def, List<AtomicEffectInstance> then, List<string> result)
        {
            foreach (var atom in then)
            {
                if (atom == null) continue;
                if (IsSelfDirectedCostAtom(atom, def))
                {
                    var cfg = ResolvePricingConfig(atom);
                    string anchor = cfg?.ManaList != null ? AnchorZh(cfg.ManaList) : "?";
                    result.Add($"效果[{def.DisplayName ?? def.Id}] 分支奖励[{cfg?.DisplayName ?? atom.Type.ToString()}]" +
                               $"（代价型·指向自己·表锚价{anchor}）——奖励发自己亏牌=自相矛盾，请核对意图");
                }
                foreach (var sub in atom.SubEffects) CheckSelfCostThen(def, new List<AtomicEffectInstance> { sub }, result);
            }
        }

        // ======================================== 代价栏翻转限制（2026-11-02 定案；2026-11-03 补③双侧域） ========================================

        /// <summary>代价栏翻转限制（2026-11-02 定案；2026-11-03 补③）：①极性=0 的原子不能进代价栏；
        /// ②没有 {target} 参数（表行 TargetKinds 空——无目标原子）的原子不能翻转成代价；
        /// ③表行 TargetKinds 只含单侧条目（纯己方/纯对方，SideLock=±1）的原子不能作代价——
        /// 代价只收同时含己方与对方条目的双侧行（2026-11-03 定案，判定与 <see cref="PayloadCostDomain"/> 同源）。
        /// 覆盖面（2026-11-08 代价效果级化）：各效果 Costs 行（卡级行已整删——代价只挂效果）。
        /// 合成器放置口经 <see cref="PayloadCostDomain"/> 早已收口（eligible=false 拒入）；
        /// 本口补**数据层显式上报**：装载期 ConvertPayloadForDisplay 剔除①②违约原子只留一行 Error
        ///（③暂不进装载剥除口——存量教学/训练数据带单侧代价行十余处，剥除会使这些卡代价当场失效，
        /// 待表侧整改后收口）；违规卡本身须被点名（卡名由消费方前缀），不得静默。
        /// 消费方：重推工具 RegenOneDeck（逐卡 LogError）与端到端验证（Assert 0 命中）——
        /// 与 <see cref="SelfCostViolations"/> 双挂接同款。</summary>
        public static List<string> PayloadCostViolations(CardData card)
        {
            var result = new List<string>();
            if (card == null) return result;
            if (card.Effects != null)
                foreach (var fx in card.Effects)
                    if (fx != null)
                        CheckPayloadCostRows(fx.Costs, $"效果[{fx.DisplayName ?? fx.Id}]", result);
            return result;
        }

        /// <summary>代价行扫描体（PayloadCostViolations 单源）：逐原子按①极性0/②无目标/③域单侧上报。</summary>
        private static void CheckPayloadCostRows(List<CostEntry> rows, string where, List<string> result)
        {
            if (rows == null) return;
            foreach (var row in rows)
            {
                if (row == null) continue;
                foreach (var pe in row.AllPayloads())
                {
                    if (pe == null || string.IsNullOrEmpty(pe.refId)) continue; // 空 refId=非 Payload 条目占位
                    var cfg = AtomicEffectTable.GetByHashId(pe.refId);
                    if (cfg == null) continue; // 悬空引用另有装载校验点名
                    float p = Math.Clamp(cfg.Polarity, -1f, 1f);
                    if (p == 0f)
                        result.Add($"{where} 代价栏原子[{cfg.DisplayName}#{pe.refId}] 极性=0" +
                                   "——极性0的原子不能进代价栏（2026-11-02 定案），请移出或换非中性行");
                    else if (cfg.GetTargetKindList().Count == 0)
                        result.Add($"{where} 代价栏原子[{cfg.DisplayName}#{pe.refId}] 无 {{target}} 参数" +
                                   "——没有目标参数的原子不能翻转成代价（代价须可指向受损方），请移出或换带目标行");
                    else if (SideLock(cfg.GetTargetKindList()) != 0)
                    {
                        string kindsZh = string.Join(",", cfg.GetTargetKindList()
                            .Select(k => TargetKindRules.ZhNameOf((TargetKind)k)));
                        result.Add($"{where} 代价栏原子[{cfg.DisplayName}#{pe.refId}] 域单侧（{kindsZh}）" +
                                   "——只能作用于单方面的原子不能作代价（2026-11-03 定案），请换 TargetKinds 含己方与对方的双侧行");
                    }
                }
            }
        }


        /// <summary>计价取行（2026-10-03 行身份修复）：同枚举多行（变体行各自锚价——如规则光环 7 行、
        /// 光环关键词/指示物族）时按实例来源行（RowHashId，converter 填）取锚；null/缺行回落 GetByType
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

        /// <summary>卡是否多模式（2026-10-31 P12 抉择默认化：仅法术可 &gt;1——模式=非充能主动效果数；
        /// 旧「内嵌 Choice 步骤」整链退役，本口=GetModeCount&gt;1 语义不变）。</summary>
        public static bool HasChoiceEffect(CardData card)
        {
            return GetModeCount(card) > 1;
        }

        /// <summary>
        /// 法术模式数（抉择=默认使用方式）：法术=主动效果数（1~3），Math.Max(1, n)；
        /// 单主动法术=1（跳过模式窗直放）。非法术恒 1（生物/结界无模式；旧 Choice 步骤通道退役）。
        /// 不再扫描 EffectStepData.choices。
        ///（P12「非充能」限定已随 2026-11-05 充能系统动作化改版失效——充能不再是卡上效果。）
        /// </summary>
        public static int GetModeCount(CardData card)
        {
            if (card?.Effects == null) return 1;
            if (card.Supertype != Cardtype.Spell) return 1;
            int n = 0;
            foreach (var fx in card.Effects)
                if (fx != null) n++;
            return Math.Max(1, n);
        }

        /// <summary>法术可施放效果列表（序=Effects 声明序——模式序号 modeIndex 索引本列表；
        /// SpellCastAnchor/ResolveSpellEffects/模式窗/AI 全消费面同源）。
        /// 非法术返回原列表；空表安全。
        ///（P12「过滤充能效果」已随 2026-11-05 充能系统动作化改版失效——充能不再是卡上效果。）</summary>
        public static List<CardEffectData> GetCastableEffects(CardData card)
        {
            var list = card?.Effects;
            if (list == null) return new List<CardEffectData>();
            if (card.Supertype != Cardtype.Spell) return list;
            var castable = new List<CardEffectData>();
            foreach (var fx in list)
                if (fx != null) castable.Add(fx);
            return castable;
        }

        /// <summary>
        /// 效果槽位数（2026-09-21 抉择分支计槽定案）：每个效果 1 槽，Choice 步骤每多一个分支再 +1 槽
        /// （抉择装两个效果，收两次槽位费）。供 CardCompositionCost 底盘结构项消费。
        /// 主动照常计槽（2026-10-02 用户定案：「不占费用，占技能挂载」维持——效果槽是 AI 侧
        /// 每卡最大原子数限制的可见性锚，免槽会让 AI 看不见超限的主动堆叠）；
        /// 主动占用的只是底盘免费额度抵扣位，元素锚价仍构筑期全免（运行时现付）。
        /// 代价**不计槽**（2026-11-02 减费三选一改版定案）：代价=并列槽位添加、不占灰槽位费——
        /// 卡级行（PayloadCosts）与效果级行（Costs）一律不计（旧「每条已填代价行各计 1 槽」随批退役）。
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
                    slots += 1; // 每效果 1 槽（含充能效果——P12：充能照常计槽；Choice 分支计槽已随 kind==2 退役删除）
                }
            }
            return slots;
        }

        /// <summary>数据级主动判定（镜像 CardEffectConverter.ConvertOne 的时机解析：Activate_* 三档时机，
        /// 或 Voluntary 声明——双向钉死规则下后者也被钉回 Activate_*）。槽位计数/施放期代价分桶
        ///（CardCostService.CollectCostEntries）等构筑期口径专用；运行时以转换后 def.IsActivatedEffect 为准。</summary>
        public static bool IsActivatedEffectData(CardEffectData eff)
        {
            int t = eff.TriggerTiming;
            TriggerTiming timing;
            if (t >= 0 && Enum.IsDefined(typeof(TriggerTiming), t)) timing = (TriggerTiming)t;
            else if (t < 0) timing = TriggerTiming.Activate_Active; // 未声明哨兵（converter 同口径回落主动）
            else timing = TriggerTiming.OnPlay;                      // 越界回退（converter 告警口径）
            if (timing == TriggerTiming.OnTurnStart) timing = TriggerTiming.OnStandbyPhase; // 系统专用归一
            bool timingActive = timing == TriggerTiming.Activate_Active
                                || timing == TriggerTiming.Activate_Instant
                                || timing == TriggerTiming.Activate_Response;
            return timingActive || eff.ActivationType == (int)EffectActivationType.Voluntary;
        }
    }
}
