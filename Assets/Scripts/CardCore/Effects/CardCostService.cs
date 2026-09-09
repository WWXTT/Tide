using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using UnityEngine;

namespace CardCore
{
    /// <summary>一行计价明细：标签 + 数值 + 颜色桶 + 阶段（S/K/E/f/Req/O）。</summary>
    public sealed class CostBreakdownLine
    {
        public string Label;
        public float Value;
        public ManaType? Color;
        public string Stage;

        public CostBreakdownLine(string stage, string label, float value, ManaType? color = null)
        {
            Stage = stage;
            Label = label;
            Value = value;
            Color = color;
        }
    }

    /// <summary>统一计价结果 —— 规则一·平衡的完整输出。</summary>
    public sealed class CardCostResult
    {
        /// <summary>声明档位 C（costList 总和；0=未声明）。</summary>
        public int DeclaredTier;

        /// <summary>建议档位 Ĉ = min{C∈[1..MaxTier] : D(C) ≤ C}（无满足则 MaxTier）。</summary>
        public int SuggestedTier;

        /// <summary>推导价 D(C)，逐色 AwayFromZero 取整。</summary>
        public Dictionary<ManaType, int> DerivedCost = new Dictionary<ManaType, int>();

        /// <summary>建议采纳的费用分布 = D(Ĉ) 逐色取整（总和 ≤ Ĉ，按实际价值采纳不强行补齐）。</summary>
        public Dictionary<ManaType, int> SuggestedCost = new Dictionary<ManaType, int>();

        /// <summary>身材费 S（灰桶）。</summary>
        public int S;

        /// <summary>关键词费 K（Grant 原子固定费合计）。</summary>
        public int K;

        /// <summary>效果锚价 E（DeriveElementCosts 口径合计）。</summary>
        public int EAnchor;

        /// <summary>挂载折扣因子 f（法术恒 1）。</summary>
        public float Factor;

        /// <summary>挂载效果定义数 N_active。</summary>
        public int ActiveEffectCount;

        /// <summary>D 逐色取整后的总和。</summary>
        public int DerivedTotal;

        /// <summary>抵扣需求 Req = max(0, D_total − C_total)。无"超模"态：不足即不符合规则一。</summary>
        public int OffsetRequirement;

        /// <summary>卡上代价提供的元素当量 O（按 CardCostConfig 当量表换算）。</summary>
        public float OffsetProvided;

        /// <summary>符合规则一 ⟺ O ≥ Req。</summary>
        public bool Conformant;

        /// <summary>逐行明细（S / K 逐关键词 / E 逐效果 / f / d(C) / Req / O）。</summary>
        public List<CostBreakdownLine> Breakdown = new List<CostBreakdownLine>();
    }

    /// <summary>
    /// 统一卡牌计价服务 —— 规则一·平衡的唯一推导入口（UI 建议价与校验共用，不再有第二套公式）。
    ///
    ///   锚价（即时价，法术定价）= CostDerivationService.DeriveElementCosts（原样复用，含持续折扣）。
    ///   身材费 S(灰)  = (攻+血) / StatUnit（StatUnit=2：1费=2属性）。
    ///   关键词费 K    = Σ 关键词对应 Grant 原子的 BaseCost（固定费，颜色=原子亲和；White/Black 归灰）。
    ///   挂载折扣 f    = 法术恒 1；随从 d(C)——落地时间（费用C=最早第C回合落地，延迟C−1）按延迟贬值。
    ///                  （原 ExtraActivationSlope×(N_active−1) 存活期望折已删——与卡层挂载口计价重复。）
    ///   卡层组合费用  = CardCompositionCost（第三层：抉择价差溢价 / 效果挂载口 ±灰，不参与 f）。
    ///   D(C) = S灰 + 挂载包×f + 卡层调整（减免发生在组合层：包总额一次取整，最大余数法分色）；D 只算一次，不迭代。
    ///   档位 C = costList 总和，是推导的输入；支付/地牌指示物/UI/召唤门槛照读声明值，不受 D 影响。
    ///   抵扣需求 Req = max(0, D_total − C_total)，由卡上代价在构筑期抵消（O ≥ Req 即符合规则一）。
    /// </summary>
    public static class CardCostService
    {
        /// <summary>主入口：以声明档位 C 为输入推导完整结果。</summary>
        public static CardCostResult Derive(CardData card)
        {
            var result = new CardCostResult();
            if (card == null) return result;

            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig();
            var cc = cfg.CardCostConfig;
            var dd = cfg.DelayDiscountConfig;

            result.DeclaredTier = Mathf.RoundToInt(card.Cost?.Values.Sum() ?? 0f);
            bool isSpell = card.Supertype == Cardtype.Spell;

            // ---- 1) 身材费 S（灰桶；身材不是挂载效果，不参与 f）----
            float statValue = ComputeStatValue(card, cc, result.Breakdown);

            // ---- 2) 关键词费 K（Grant 原子固定费，无 value 缩放）----
            float keywordTotal;
            var kwBuckets = ComputeKeywordBuckets(card, result.Breakdown, out keywordTotal);

            // ---- 2b) 连接光环费 A（三轨制 2026-09-09）：linkAuras 按**单回合指示物档**计价
            //（来源须持续在场——断链/离场即失效的折价定案），并入关键词桶同级参与挂载折扣与取整 ----
            float auraTotal;
            var auraBuckets = ComputeLinkAuraBuckets(card, result.Breakdown, out auraTotal);
            foreach (var kv in auraBuckets)
            {
                kwBuckets.TryGetValue(kv.Key, out var prevKw);
                kwBuckets[kv.Key] = prevKw + kv.Value;
            }
            keywordTotal += auraTotal;

            // ---- 3) 效果锚价 E（经 CardEffectConverter，与运行时执行同口径）----
            // 启动式能力（Activate_*，2026-09-08 定案）：构筑期不占卡费——元素锚价运行时现付
            // （发动 = 横置 + 扣锚价 + 选目标），不进 E 桶；但仍占技能挂载口（MountSlotAdjust
            // 按 Effects 条数计，含启动式——「不占费用，占技能挂载」的定案两侧在此闭合）。
            float effectTotal = 0f;
            var effBuckets = new Dictionary<ManaType, float>();
            var effectDefs = CardEffectConverter.ConvertAll(card.Effects, card.ID);
            foreach (var def in effectDefs)
            {
                if (def == null) continue;
                result.ActiveEffectCount++;
                if (def.IsActivatedEffect)
                {
                    result.Breakdown.Add(new CostBreakdownLine("E", $"启动式 {def.DisplayName ?? def.Id}（构筑期不计锚价，运行时现付；占挂载口）", 0f));
                    continue;
                }

                float defTotal = 0f;
                foreach (var cost in CostDerivationService.DeriveElementCosts(def, 0, isSpell)) // 法术宿主按永久档计（三轨制定案）
                {
                    effBuckets.TryGetValue(cost.ManaType, out var prev);
                    effBuckets[cost.ManaType] = prev + cost.Value;
                    effectTotal += cost.Value;
                    defTotal += cost.Value;
                }
                result.Breakdown.Add(new CostBreakdownLine("E", $"效果 {def.DisplayName ?? def.Id} (锚价)", defTotal));
            }

            // ---- 4) 挂载折扣 f（法术不折；档位未声明时按上限档计，供建议档位推导用）----
            float factor = ComputeMountFactor(card, cc, dd, result.DeclaredTier);
            {
                int tierForFactor = result.DeclaredTier > 0
                    ? Mathf.Clamp(result.DeclaredTier, 1, cc.MaxTier)
                    : cc.MaxTier;
                float d = isSpell ? 1f : dd.At(tierForFactor);
                result.Factor = factor;
                result.Breakdown.Add(new CostBreakdownLine("f", $"挂载折扣 f（d({tierForFactor})={d:0.###}）", factor));
            }

            // ---- 5) L2 组合完成 + L3 整卡延迟折（2026-09-07 定案：d(C) 对完成的卡最后一步打折）----
            // L2：E/K 汇桶 + S/挂载口入灰桶（挂载口是 L2 最后一步）；
            // L3：整卡（S+E+K+卡层调整）×f 一次取整，最大余数法分色（减免发生在组合层，非逐效果）。
            var kwMultiplier = cc.KeywordsShareDelayDiscount ? factor : 1f;
            int statGray = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            int mountAdj = CardCompositionCost.MountSlotAdjust(card);
            if (mountAdj != 0)
                result.Breakdown.Add(new CostBreakdownLine("M",
                    $"挂载口调整（效果 {card.Effects?.Count ?? 0} 个 vs 基线）", mountAdj, ManaType.Gray));

            var mounted = ApportionMounted(effBuckets, kwBuckets, kwMultiplier, factor, statGray + mountAdj);

            foreach (var kv in mounted)
            {
                result.DerivedCost[kv.Key] = kv.Value;
                result.Breakdown.Add(new CostBreakdownLine("D", $"推导费 {kv.Key} ×{kv.Value}", kv.Value, kv.Key));
            }
            result.S = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            result.K = (int)Math.Round(keywordTotal, MidpointRounding.AwayFromZero);
            result.EAnchor = (int)Math.Round(effectTotal, MidpointRounding.AwayFromZero);
            result.DerivedTotal = result.DerivedCost.Values.Sum();

            // ---- 6) 抵扣需求与代价当量 ----
            result.OffsetRequirement = Mathf.Max(0, result.DerivedTotal - result.DeclaredTier);
            result.OffsetProvided = OffsetProvided(card, cc);
            result.Conformant = result.OffsetProvided >= result.OffsetRequirement;
            result.Breakdown.Add(new CostBreakdownLine("Req", $"抵扣需求 max(0, D{result.DerivedTotal} − C{result.DeclaredTier})", result.OffsetRequirement));
            result.Breakdown.Add(new CostBreakdownLine("O", $"代价当量合计（已提供）", result.OffsetProvided));

            // ---- 7) 建议档位 Ĉ 与建议分布（含卡层挂载口调整——建议价与 D 同口径）----
            result.SuggestedTier = FindSuggestedTier(card, cc, dd, isSpell, statValue, kwBuckets, effBuckets, mountAdj);
            result.SuggestedCost = BuildCostAtTier(result.SuggestedTier, cc, dd, isSpell, statValue, kwBuckets, effBuckets, mountAdj);
            return result;
        }

        /// <summary>缺省建议：建议档位 Ĉ 的多色分布（CardLoader 兜底 / 重生成菜单用）。</summary>
        public static Dictionary<ManaType, int> DeriveSuggestedCost(CardData card)
        {
            var r = Derive(card);
            return r.SuggestedCost;
        }

        /// <summary>
        /// 幂等兜底：Cost 为空且非超量（费用由素材推导）→ 写入建议档位分布；非空一律不动。
        /// D=0（无身材无效果无关键词）保持空 —— PlayCard 的 {Gray:1} 默认兜底行为不变。
        ///
        /// 抉择卡（2026-09-07 定案）：①构筑期推导全部模式费写入 ModeCostCache（发动时只读不重推导）；
        /// ②声明 costList 缺省时写**最大模式费**——「作为地牌取最大」，地牌产元素/召唤素材/UI 等
        /// 声明值消费面零改动；支付仍按所选模式（GameActions.GetCardCost 走 GetModeCost）。
        /// 声明值已存在则只填缓存不动声明（手写费用=地牌值，声明优先惯例延续）。
        /// </summary>
        public static void EnsureCost(CardData card)
        {
            if (card == null) return;

            if (CostDerivationService.HasChoiceEffect(card))
            {
                var modes = DeriveModeCosts(card);
                if (card.Cost != null && card.Cost.Count > 0)
                {
                    if (modes.Count > 0) card.ModeCostCache = modes; // 声明优先：只填缓存
                    return;
                }
                if ((card.Subtype & CardSubtype.Xyz) != 0)
                {
                    if (modes.Count > 0) card.ModeCostCache = modes;
                    return;
                }
                if ((card.Subtype & CardSubtype.Ritual) != 0)
                {
                    if (modes.Count > 0) card.ModeCostCache = modes; // 仪式保持空 Cost
                    return;
                }

                var maxCost = MaxModeCost(modes);
                if (maxCost.Count > 0)
                {
                    card.Cost = maxCost;
                    card.ResetCache(); // 会清缓存，最后回填
                }
                if (modes.Count > 0) card.ModeCostCache = modes;
                return;
            }

            if (card.Cost != null && card.Cost.Count > 0) return;      // 严格非空即返：禁止覆盖声明费用
            if ((card.Subtype & CardSubtype.Xyz) != 0) return;
            if ((card.Subtype & CardSubtype.Ritual) != 0) return;      // 仪式：0 费说明书卡，保持空 Cost（打出免费）

            var suggested = DeriveSuggestedCost(card);
            if (suggested.Count == 0) return;

            card.Cost = suggested.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value);
            card.ResetCache();
        }

        // ======================================== 抉择 per-mode 计价 ========================================

        /// <summary>
        /// 构筑期推导全部模式费用（与 Derive 同口径：L1 原子锚价 → L2 组合含挂载口 → L3 整卡延迟折）。
        /// 两遍式：第一遍取各模式效果锚桶（未折未取整）——价差溢价与「模式0=最高消耗」契约都按原始锚价判；
        /// 第二遍组装（S/挂载口/价差入灰桶，与效果同吃 d(C)——整卡最后折）。无抉择卡返回空表。
        /// </summary>
        public static List<Dictionary<int, float>> DeriveModeCosts(CardData card)
        {
            var result = new List<Dictionary<int, float>>();
            int modeCount = CostDerivationService.GetModeCount(card);
            if (card == null || modeCount <= 1) return result;

            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig();
            var cc = cfg.CardCostConfig;
            var dd = cfg.DelayDiscountConfig;
            bool isSpell = card.Supertype == Cardtype.Spell;
            int declaredTier = Mathf.RoundToInt(card.Cost?.Values.Sum() ?? 0f);

            // 模式无关三块（与 Derive 共用助手，防口径漂移；无 breakdown 记录）
            float statValue = ComputeStatValue(card, cc, null);
            float keywordTotal;
            var kwBuckets = ComputeKeywordBuckets(card, null, out keywordTotal);

            // 连接光环费并入关键词桶（与 Derive 的 2b 同口径）
            float auraTotal;
            foreach (var kv in ComputeLinkAuraBuckets(card, null, out auraTotal))
            {
                kwBuckets.TryGetValue(kv.Key, out var prevKw);
                kwBuckets[kv.Key] = prevKw + kv.Value;
            }
            keywordTotal += auraTotal;

            var effectDefs = CardEffectConverter.ConvertAll(card.Effects, card.ID);
            float factor = ComputeMountFactor(card, cc, dd, declaredTier);
            int statGray = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            var kwMultiplier = cc.KeywordsShareDelayDiscount ? factor : 1f;
            int mountAdj = CardCompositionCost.MountSlotAdjust(card);

            // ---- 第一遍：各模式效果锚桶（DeriveElementCosts 的 modeIndex 分支，未折未取整）----
            // 启动式能力与 Derive 同口径：构筑期不占卡费（运行时现付），各模式桶一律跳过。
            var modeBuckets = new List<Dictionary<ManaType, float>>();
            var rawTotals = new List<float>();
            for (int m = 0; m < modeCount; m++)
            {
                var effBuckets = new Dictionary<ManaType, float>();
                foreach (var def in effectDefs)
                {
                    if (def == null) continue;
                    if (def.IsActivatedEffect) continue;
                    foreach (var cost in CostDerivationService.DeriveElementCosts(def, m, isSpell)) // 法术宿主按永久档计（三轨制定案）
                    {
                        effBuckets.TryGetValue(cost.ManaType, out var prev);
                        effBuckets[cost.ManaType] = prev + cost.Value;
                    }
                }
                modeBuckets.Add(effBuckets);

                float total = 0f;
                foreach (var v in effBuckets.Values) total += v;
                rawTotals.Add(total);
            }

            // 数据契约（2026-09-07 用户定案）：模式序号 0 = 最高消耗——编辑界面遵守；
            // 规则一 Derive 按首模式推导即按最大模式，口径由此闭合。违约仅警告不纠正（计价按真实数据算）。
            float tierMax = 0f;
            foreach (var t in rawTotals) if (t > tierMax) tierMax = t;
            if (rawTotals[0] < tierMax - 0.001f)
                Debug.LogWarning($"[CardCostService] 抉择卡 {card.ID} 模式0非最高消耗（{rawTotals[0]} < {tierMax}）——违反数据契约（编辑界面应把最高消耗放在序号0）");

            // 卡层组合费用：价差溢价按原始锚价差判定（S/挂载口/延迟折对模式均匀，不改变差值）
            int spreadPremium = CardCompositionCost.ChoiceSpreadPremium(rawTotals);
            float grayAdd = statGray + spreadPremium + mountAdj;

            // ---- 第二遍：L2 组合 + L3 整卡折（与 Derive 的 ApportionMounted 同口径）----
            for (int m = 0; m < modeCount; m++)
            {
                var mounted = ApportionMounted(modeBuckets[m], kwBuckets, kwMultiplier, factor, grayAdd);
                result.Add(mounted.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value));
            }
            return result;
        }

        /// <summary>
        /// 读取某模式的支付费用（构筑期缓存；缓存缺失时兜底重推导——正常路径 EnsureCost 已填）。
        /// 返回副本：调用方在其上叠加 CostUp/CostDown 指示物层，不污染缓存。
        /// 空字典=该模式免费（推导为 0 是定价结果，不落 {Gray:1} 默认）。
        /// </summary>
        public static Dictionary<int, float> GetModeCost(CardData card, int modeIndex)
        {
            if (card == null) return new Dictionary<int, float>();
            if (card.ModeCostCache == null || card.ModeCostCache.Count == 0)
                card.ModeCostCache = DeriveModeCosts(card); // 兜底：手构卡未走装载链
            if (card.ModeCostCache.Count == 0) return new Dictionary<int, float>();

            int idx = Mathf.Clamp(modeIndex, 0, card.ModeCostCache.Count - 1);
            return new Dictionary<int, float>(card.ModeCostCache[idx]);
        }

        /// <summary>最大模式费（总额最高的那套分布；全空返回空字典）。地牌值/排序键用。</summary>
        public static Dictionary<int, float> MaxModeCost(List<Dictionary<int, float>> modes)
        {
            Dictionary<int, float> best = null;
            float bestTotal = -1f;
            foreach (var m in modes)
            {
                float total = 0f;
                foreach (var v in m.Values) total += v;
                if (total > bestTotal)
                {
                    bestTotal = total;
                    best = m;
                }
            }
            return best != null ? new Dictionary<int, float>(best) : new Dictionary<int, float>();
        }

        // ======================================== 内部 ========================================

        // —— 模式无关的三块（Derive 与 DeriveModeCosts 共用，防口径漂移；breakdown 传 null 则不记行）——

        /// <summary>身材费 S：statPoints / StatUnit（灰桶，不参与 f）。</summary>
        private static float ComputeStatValue(CardData card, CardCostConfig cc, List<CostBreakdownLine> breakdown)
        {
            int statPoints = (card.Power ?? 0) + (card.Life ?? 0);
            float statValue = statPoints / Mathf.Max(1f, cc.StatUnit);
            breakdown?.Add(new CostBreakdownLine("S", $"身材费 (攻{card.Power ?? 0}/血{card.Life ?? 0})", statValue, ManaType.Gray));
            return statValue;
        }

        /// <summary>关键词费 K：Grant 原子固定费按颜色分桶（无 value 缩放）。</summary>
        private static Dictionary<ManaType, float> ComputeKeywordBuckets(CardData card,
            List<CostBreakdownLine> breakdown, out float keywordTotal)
        {
            keywordTotal = 0f;
            var kwBuckets = new Dictionary<ManaType, float>();
            if (card.Keywords != null)
            {
                CardLoader.LoadKeywords(); // 惰性建目录（GetKeywordDefinition 不自动建）
                foreach (var kwId in card.Keywords)
                {
                    if (string.IsNullOrEmpty(kwId)) continue;
                    var def = CardLoader.GetKeywordDefinition(kwId);
                    if (def == null || string.IsNullOrEmpty(def.atomicEffect)
                        || !Enum.TryParse<AtomicEffectType>(def.atomicEffect, out var grantType))
                    {
                        breakdown?.Add(new CostBreakdownLine("K", $"关键词 {kwId}（未登记 Grant 原子，计 0）", 0f));
                        continue;
                    }

                    var atomCfg = AtomicEffectTable.GetByType(grantType);
                    float baseCost = atomCfg?.BaseCost ?? 0f;
                    var color = ElementAffinities.GetAffinityForEffect(grantType).PrimaryColor;
                    kwBuckets.TryGetValue(color, out var prev);
                    kwBuckets[color] = prev + baseCost;
                    keywordTotal += baseCost;
                    breakdown?.Add(new CostBreakdownLine("K", $"关键词 {kwId} (Grant 固定费)", baseCost, color));
                }
            }
            return kwBuckets;
        }

        /// <summary>
        /// 连接光环费 A（三轨制定案 2026-09-09）：卡面 linkAuras 声明按**单回合指示物档**计价——
        /// 光环须来源持续在场（断链/离场即失效），按最便宜持续档折价（定案：来源存活条件折价）。
        /// stat 行用 ModifyPower/ModifyLife 代表原子 ×max(1,|value|)×(0.6/该原子表默认)；
        /// keyword 行经关键词目录反查 Grant 原子固定费 × 同一相对系数。
        /// 返回桶并入关键词桶（Derive 2b / DeriveModeCosts），参与挂载折扣与 L2/L3 取整。
        /// </summary>
        private static Dictionary<ManaType, float> ComputeLinkAuraBuckets(CardData card,
            List<CostBreakdownLine> breakdown, out float auraTotal)
        {
            auraTotal = 0f;
            var buckets = new Dictionary<ManaType, float>();
            if (card?.LinkAuras == null || card.LinkAuras.Count == 0) return buckets;

            var attrCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
            float singleTurn = attrCfg.GetDurationDiscount(DurationType.UntilEndOfTurn);
            CardLoader.LoadKeywords(); // 惰性建目录（keyword 行反查 Grant 原子）

            foreach (var aura in card.LinkAuras)
            {
                if (aura == null) continue;

                AtomicEffectType rep;
                int magnitude = 1;
                string label;
                if (!string.IsNullOrEmpty(aura.stat))
                {
                    bool isLife = aura.stat.Equals("Life", System.StringComparison.OrdinalIgnoreCase);
                    rep = isLife ? AtomicEffectType.ModifyLife : AtomicEffectType.ModifyPower;
                    magnitude = System.Math.Max(1, System.Math.Abs(aura.value));
                    label = $"{aura.stat}{(aura.value >= 0 ? "+" : "")}{aura.value}";
                }
                else if (!string.IsNullOrEmpty(aura.keyword))
                {
                    var def = CardLoader.GetKeywordDefinition(aura.keyword);
                    if (def == null || string.IsNullOrEmpty(def.atomicEffect)
                        || !System.Enum.TryParse<AtomicEffectType>(def.atomicEffect, out rep))
                    {
                        breakdown?.Add(new CostBreakdownLine("A", $"连接光环 {aura.keyword}（未登记 Grant 原子，计 0）", 0f));
                        continue;
                    }
                    label = aura.keyword;
                }
                else continue;

                var atomCfg = AtomicEffectTable.GetByType(rep);
                if (atomCfg == null || atomCfg.BaseCost <= 0f) continue;

                float factor = singleTurn / Mathf.Max(0.0001f, attrCfg.GetDurationDiscount(atomCfg.DurationType));
                float amount = atomCfg.BaseCost
                               * (atomCfg.CostMultiplier > 0f ? atomCfg.CostMultiplier : 1f)
                               * magnitude * factor;
                var color = ElementAffinities.GetAffinityForEffect(rep).PrimaryColor;
                buckets.TryGetValue(color, out var prev);
                buckets[color] = prev + amount;
                auraTotal += amount;
                breakdown?.Add(new CostBreakdownLine("A",
                    $"连接光环 {label}（单回合档 ×{factor:0.###}）", amount, color));
            }
            return buckets;
        }

        /// <summary>挂载折扣 f：法术恒 1；随从 d(C)。
        /// （原 ExtraActivationSlope×(N_active−1) 已删——效果多→贵由卡层挂载口规则统一承担，不再经 f 重复折。）</summary>
        private static float ComputeMountFactor(CardData card, CardCostConfig cc, DelayDiscountConfig dd,
            int declaredTier)
        {
            bool isSpell = card.Supertype == Cardtype.Spell;
            if (isSpell) return 1f;
            int tierForFactor = declaredTier > 0 ? Mathf.Clamp(declaredTier, 1, cc.MaxTier) : cc.MaxTier;
            return Mathf.Max(0f, dd.At(tierForFactor));
        }

        /// <summary>卡上代价条目 → 元素当量（构筑期抵扣换算；ElementConsume/Mill/SendExtra 是费用/游戏内机制，不当量）。</summary>
        private static float OffsetProvided(CardData card, CardCostConfig cc)
        {
            float total = 0f;
            if (card.Effects == null) return total;
            foreach (var effect in card.Effects)
            {
                if (effect?.Costs == null) continue;
                foreach (var cost in effect.Costs)
                {
                    if (cost == null) continue;
                    switch ((CostType)cost.CostType)
                    {
                        case CostType.DiscardCard: total += cc.DiscardCardValue * Mathf.Max(1, cost.Value); break;
                        case CostType.LifePayment: total += cc.LifeValuePerPoint * Mathf.Max(1, cost.Value); break;
                        case CostType.Sleep: total += cc.SleepValuePerTurn * Mathf.Max(1, cost.TurnDuration); break;
                        case CostType.SummonMaterial: total += cc.SummonMaterialValue * Mathf.Max(1, cost.Value); break;
                        case CostType.OpponentDraw: total += cc.OpponentDrawValue * Mathf.Max(1, cost.Value); break;
                        case CostType.OpponentHeal: total += cc.OpponentHealValuePerPoint * Mathf.Max(1, cost.Value); break;
                        case CostType.SelfSickness: total += cc.SelfSicknessValue * Mathf.Max(1, cost.Value); break;
                        case CostType.OpponentBuff: total += cc.OpponentBuffValue * Mathf.Max(1, cost.Value); break;
                    }
                }
            }
            return total;
        }

        /// <summary>Ĉ = min{C∈[1..MaxTier] : D(C) ≤ C}；D(C) 随 C 单调不增（d 递减），从 1 向上搜；无满足取 MaxTier。</summary>
        private static int FindSuggestedTier(CardData card, CardCostConfig cc, DelayDiscountConfig dd,
            bool isSpell, float statValue, Dictionary<ManaType, float> kwBuckets, Dictionary<ManaType, float> effBuckets, int mountAdj)
        {
            // 无身材无效果无关键词 → D=0，无需建议；返回 0，调用方按「保持空」处理。
            // （关键词存在但未登记 Grant 原子时 kwBuckets 为空 —— 仍参与搜索，D≈S。）
            if (statValue <= 0f && kwBuckets.Count == 0 && effBuckets.Count == 0
                && (card.Keywords == null || card.Keywords.Count == 0))
                return 0;

            for (int tier = 1; tier <= cc.MaxTier; tier++)
            {
                var costAt = BuildCostAtTier(tier, cc, dd, isSpell, statValue, kwBuckets, effBuckets, mountAdj);
                if (costAt.Values.Sum() <= tier) return tier;
            }
            return cc.MaxTier;
        }

        /// <summary>按指定档位构建 D(tier) 的逐色分布（L2 组合 + L3 整卡折 + 组合层取整；供建议档位搜索与采纳写入）。</summary>
        private static Dictionary<ManaType, int> BuildCostAtTier(int tier, CardCostConfig cc, DelayDiscountConfig dd,
            bool isSpell, float statValue, Dictionary<ManaType, float> kwBuckets, Dictionary<ManaType, float> effBuckets, int mountAdj)
        {
            float d = isSpell ? 1f : dd.At(Mathf.Clamp(tier, 1, cc.MaxTier));
            float factor = Mathf.Max(0f, d);

            var kwMultiplier = cc.KeywordsShareDelayDiscount ? factor : 1f;
            int statGray = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            return ApportionMounted(effBuckets, kwBuckets, kwMultiplier, factor, statGray + mountAdj);
        }

        /// <summary>
        /// 组合层计价（L3 整卡折口径）：效果桶×f + 关键词桶×kwMultiplier + 灰桶（S+卡层调整）×f
        /// 汇总为总额——一次 AwayFromZero 取整（减免发生在组合层，不做逐效果取整），
        /// 最大余数法把整数实付分摊回颜色（余数大者优先，并列取桶值大者，再并列按枚举序）。
        /// grayAdd：身材费与卡层组合调整之和——整卡最后折定案下与效果同吃 d(C)。
        /// </summary>
        private static Dictionary<ManaType, int> ApportionMounted(Dictionary<ManaType, float> effBuckets,
            Dictionary<ManaType, float> kwBuckets, float kwMultiplier, float factor, float grayAdd = 0f)
        {
            var mounted = new Dictionary<ManaType, float>();
            foreach (var kv in effBuckets)
            {
                mounted.TryGetValue(kv.Key, out var prev);
                mounted[kv.Key] = prev + kv.Value * factor;
            }
            foreach (var kv in kwBuckets)
            {
                mounted.TryGetValue(kv.Key, out var prev);
                mounted[kv.Key] = prev + kv.Value * kwMultiplier;
            }
            if (grayAdd != 0f)
            {
                mounted.TryGetValue(ManaType.Gray, out var prev);
                mounted[ManaType.Gray] = prev + grayAdd * factor; // 整卡折：S/卡层调整同吃 d(C)
            }

            float total = 0f;
            foreach (var kv in mounted) total += kv.Value;
            int payInt = (int)Math.Round(total, MidpointRounding.AwayFromZero);

            var result = new Dictionary<ManaType, int>();
            int floorSum = 0;
            var fracs = new List<(ManaType color, float frac, float bucket)>();
            foreach (var kv in mounted)
            {
                int fl = (int)Math.Floor(kv.Value);
                result[kv.Key] = fl;
                floorSum += fl;
                fracs.Add((kv.Key, kv.Value - fl, kv.Value));
            }

            int remainder = payInt - floorSum;
            foreach (var t in fracs.OrderByDescending(x => x.frac).ThenByDescending(x => x.bucket).ThenBy(x => (int)x.color))
            {
                if (remainder <= 0 || t.frac <= 0f) break;
                result[t.color] += 1;
                remainder--;
            }
            if (remainder > 0) // 兜底（浮点尾差理论不可达）：加给最大桶
            {
                var maxKey = mounted.OrderByDescending(x => x.Value).First().Key;
                result[maxKey] += remainder;
            }

            foreach (var k in result.Where(x => x.Value <= 0).Select(x => x.Key).ToList()) result.Remove(k);
            return result;
        }
    }
}
