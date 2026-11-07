using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 卡层组合费用（「效果→卡」组合层，2026-09-07 定案）——独立的第三层计价：
    /// · 原子层（CostDerivationService）：原子→效果锚价（并发取总 / 条件奖励免费 / 抉择 per-mode）；
    /// · 规则一（CardCostService.Derive）：声明档位推导 D(C) 与代价抵扣校验；
    /// · 本层：只对「卡的结构性弹性」收额外费——玩家为灵活性付费，与效果本身的强度无关。
    ///
 /// 当前规则（2026-10-15 卡型默认分配定案 + 2026-10-15 深夜三类卡改版；速度口径 2026-10-31 P12 回归）：
 /// 效果组合界面不再能设置发动速度；三个免费额度（底盘 = 显性三万能色：入组 1 + 抽牌 2）按卡型默认分配：
 /// · 法术 = 速度 3−槽数（2026-10-31 P12 回归，P6c 5−N 退役）：1 槽=速2、2 槽=速1、3 槽=速0、
 ///   4 槽=速0（槽含充能效果——充能照常计 1 槽；**代价行不计槽**——2026-11-02 减费三选一改版：
 ///   代价=并列槽位添加不占槽位费，挂代价不再压速度；
 ///   速度不收钱、卡费一分不变，只变速度门），动态计算不落字段（KindSpeedBonusOf 单源；
 ///   SpeedBonus 字段退役）。
 ///   法术只挂 1~3 个主动效果（发动时弹窗抉择其一）：出牌费不含效果锚价，
 ///   发动=现付所选效果锚价（cast 结算步并入账单），无额外费用。
 ///   效果发动速度=卡的发动速度（法术专享通道）；从手牌打出即按此速度过门（GetCardCastSpeed）。
 /// · 结界禁主动效果（只挂自动/光环）：速度通道退役（EnchantSpeedBonusOf 删除），
 ///   免费点折免费耐久底（FreeDurabilityBaseOf=剩余÷耐久单价；2026-11-04 碑族0费改版——
 ///   旧「入场+N 堆叠」（DurabilityBonusOf）退役：碑按印刷耐久入场，底内免卡价）；
 ///   耐久=受击/被动触发次数池（主动−1 通道退役）。
 /// · 生物 = 攻击 1 + 守卫 1 + 每个效果槽 1——**结构项>3 的差额入灰**（2026-10-22 最终定案；
 ///   2026-11-06 opt-in 改版：攻/守=卡面声明才有，资格缺位各释放 1 点）：
 ///   纯白板（攻+守=2 项）盈余 1 点 → +1/+1 属性；只声明攻（红套火种=攻 1 项，
 ///   盈余 2 点 → +2/+2 属性）；3 效果+攻+守=5 项 → 2 灰槽位费。
 ///   充能资格（2026-11-06）不占结构项：开启=推导费固定 +1 灰（CardCostService 三口），底盘分配不动。
 ///   效果槽与攻/守同占 3 点底盘，不再“效果槽不占额度、每槽单独加价”。
 ///   生物禁挂连接光环（2026-10-15 改版：只留主动/自动/规则光环/本体关键词）。
 /// · 生物和结界从手牌使用的默认速度是 0（仅己方主要阶段，目前无效果能改）。
 /// ①底盘结构项（占额度）：生物 = 攻/守在场各占 1 + 效果槽 ×CountEffectSlots；
 ///   非生物 = 效果槽 ×CountEffectSlots（抉择分支计槽 2026-09-21 维持；主动照常计槽；
 ///   代价行不计槽——2026-11-02 减费三选一改版：代价不再占 1 灰槽位费）。
 /// ②免费点 N = 3 − 结构项（≥0），按卡型固定去向：法术 → 提速（N=槽数余量，槽位从速度扣）；
 ///   结界 → 折免费耐久底（⌊N÷DurabilityUnitCost⌋，计价侧免除、入场耐久=印刷值下限1——
 ///   2026-11-04 碑族0费改版，取代旧「全入耐久=入场+N」）；
 ///   地牌/其余 → 全额抵价（ReduceBuckets 先灰后最高色）；
 ///   生物 → **免费属性底 n/n**（2026-11-03 底盘分配改版）：预算优先占坑 攻1+守1+**首个效果槽**，
 ///   剩余 n=3−(攻+守+首槽) 落成攻/血各 n 点的免费底——计价侧（StatBucketsOf/ComputeStatValue）
 ///   直接免除前 n 点，超出部分按颜色收费（攻红/血绿，真实小数 0.5/点不参与整卡取整，支付侧逐色进 1）。
 ///   免费底固定 n/n 不可偏移（组合器以 n 为属性输入下限——无法做成 5-1 之类偏斜底）。
 ///   旧「盈余点先红后绿扣色桶」（StatOffsetOf）随之退役。
 /// ③超槽加价（2026-11-03 生物改版）：生物 = **首个效果槽之外每槽恒 +1 灰**（攻/守占坑不触发加价；
 ///   旧「结构项>3 差额」口径对攻守齐全生物等价、对 opt-out 生物少收——新口径统一按槽数计）；
 ///   非生物保持 结构项>3 差额 ×1 入灰；取整后入灰（不吃 d(C)）。
 /// ④抵价落位：非生物 d(C) 取整后总价逐点扣，先灰后最高费用色（ReduceBuckets）；
 ///   生物无抵价步（免费属性底已在计价桶内免除）。
    /// ⑤衍生物无底盘（CardCostService.ContentPriceOf/ByColor 口径）：未走「入组/抽牌」通道，
    ///   结构费全额计（攻/守/槽全算）、无免费点可支配——见 CostDerivation SummonToken 分支。
    ///
    /// 应用点：CardCostService.Derive/DeriveModeCosts 统一附加；
    /// 运行时消费口：GameActions（法术提速）/ EquipRules·MorphSystem（结界入场耐久=
    /// 印刷值·TotalDurabilityOf 单源）/ CardCostService（免费耐久底·FreeDurabilityBaseOf）。
    /// 参数见 ValueSystemRuntimeConfig.CardCompositionConfig（表 Category=CardComposition）。
    /// </summary>
    public static class CardCompositionCost
    {
        /// <summary>结构项数（未乘费率；2026-10-22 最终定案；2026-11-06 opt-in 同构翻转）：
        /// **统一 = 攻/守/效果槽各占 1 点**——生物 = 已声明攻 + 已声明守 + 效果槽
        ///（CountEffectSlots，抉择分支计槽；攻/守资格缺位各释放 1 点）；非生物 = 效果槽。
        /// 三点免费底盘被占用后的盈余按卡型分配，结构项>3 的差额入灰加价。
        /// 充能资格不占结构项（固定 +1 灰加价走 CardCostService——不动底盘分配）。</summary>
        public static int ChassisItems(CardData card)
        {
            int slots = CostDerivationService.CountEffectSlots(card);
            if (card != null && card.Supertype == Cardtype.Creature)
                return (card.AttackAbility ? 1 : 0) + (card.GuardAbility ? 1 : 0) + slots;
            return slots;
        }

        /// <summary>
        /// 超槽加价（恒 ≥0、永不退费，取整后入灰不吃 d(C)）；2026-10-22 最终定案统一为
        /// **结构项 × 费率 − 3 点额度**（生物/非生物同式）：
        /// 生物 3 效果 + 攻 + 守 = 5 项 → 2 灰（用户点名口径）；
        /// 1 效果生物 = 3 项 → 0 灰（与 SurplusOf=0 对冲，净 0）；
        /// 2 效果生物 = 4 项 → 1 灰；纯白板 = 2 项（攻+守）→ 0 灰且盈余 1 点入属性。
        /// 攻/守 = 卡面声明才有（2026-11-06 opt-in 改版——资格缺位释放 1 点；Power=0 不自动剥夺）。
        /// </summary>
        public static int StructuralSurcharge(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            // 生物（2026-11-03 底盘分配改版）：首槽上预算，其余槽恒 1 灰/个——攻/守占坑不再触发加价
            if (card != null && card.Supertype == Cardtype.Creature)
                return Math.Max(0, CostDerivationService.CountEffectSlots(card) - 1) * rate;
            return Math.Max(0, ChassisItems(card) * rate - budget);
        }

        /// <summary>
        /// 免费点 N（恒 ≥0）：3 点额度被结构项占用后的盈余，按卡型固定去向（类头②）——
        /// 法术=提速 / 结界=折免费耐久底（FreeDurabilityBaseOf）/ 生物=免费属性底（FreeAttrBaseOf）/ 地牌=全额抵价。
        /// </summary>
        public static int SurplusOf(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            return Math.Max(0, budget - ChassisItems(card) * rate);
        }

        /// <summary>计价侧抵价点数（ApplyChassis 消费·ReduceBuckets 先灰后最高色）：结界/生物/法术 = 0
        ///（结界走免费耐久底、生物走免费属性底、法术走提速——均已在各自计价口内免除，无剩余抵价）；
        /// 地牌/其余 = N。</summary>
        public static int PriceOffsetOf(CardData card)
        {
            if (card == null) return 0;
            int surplus = SurplusOf(card);
            if (surplus <= 0) return 0;
            if (card.Supertype == Cardtype.Enchantment) return 0;
            if (card.Supertype == Cardtype.Creature) return 0;
            if (card.Supertype == Cardtype.Spell) return 0;
            return surplus;
        }

        /// <summary>生物免费属性底 n（2026-11-03 底盘分配改版）：3 点预算优先占坑
        /// 攻 1 + 守 1 + **首个效果槽**，剩余 n（≥0）= 攻/血各 n 点的免费底——计价侧
        /// （StatBucketsOf/ComputeStatValue）直接免除前 n 点，超出部分按颜色收费；
        /// 组合器以本值为属性输入下限（免费底固定 n/n 不可偏移，玩家只能在底之上加付属性，
        /// 无法设置成 5-1 之类偏斜底）。非生物恒 0。旧「盈余点先红后绿扣色桶」（StatOffsetOf）随之退役。</summary>
        public static int FreeAttrBaseOf(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Creature) return 0;
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            int slots = CostDerivationService.CountEffectSlots(card);
            int items = (card.AttackAbility ? 1 : 0) + (card.GuardAbility ? 1 : 0) + Math.Min(1, slots);
            return Math.Max(0, budget - items * rate);
        }

        /// <summary>卡型速度加成单源（2026-10-15 定案：速度归卡型免费额度默认分配；
        /// 2026-10-29 P6c 曾改 5−槽数，**2026-10-31 P12 回归 3−槽数**）：
        /// 法术 = max(0, 3 − 效果槽数)（CountEffectSlots 含充能效果——充能照常计 1 槽；
        /// 代价行不计槽——2026-11-02 减费三选一改版：代价不占槽不压速度；
        /// 1 槽=速2、2 槽=速1、3 槽=速0、4 槽=速0；速度不收钱，卡费不变只变速度门，动态计算不落字段，
        /// SpeedBonus 字段已退役不再消费）；结界/生物/地牌 = 0（结界主动只收充能（P12）且充能恒 0 速；
        /// 生物/结界从手牌使用恒 0 速）。
        /// 注意：**不动 SurplusOf**——免费点仍管生物属性抵扣 / 结界耐久 / 地牌抵价（改它会连带改价）。
        /// 消费口：GameActions.GetCardEffectDefinitions（效果 BaseSpeed 加和）、
        /// SpeedCalculator.GetCardCastSpeed（法术整卡施放速度门）、AtomText.ActivationPrefix（句首显示）。</summary>
        public static int KindSpeedBonusOf(CardData card)
        {
            if (card == null) return 0;
            if (card.Supertype == Cardtype.Spell)
                return Math.Max(0, 3 - CostDerivationService.CountEffectSlots(card));
            return 0;
        }

        /// <summary>结界免费耐久底（2026-11-04 碑族0费改版）：免费点按耐久单价折算的免费耐久数
        /// ⌊SurplusOf ÷ DurabilityUnitCost⌋——1 槽碑=免费点 2 → 底 4（印刷耐久 4 内免卡价，
        /// 超出部分 0.5蓝/点计价）。耐久单价≤0（全局免费耐久）→ 底=印刷值全免。非结界恒 0。
        /// 计价消费：CardCostService.ComputeStatValue / StatBucketsOf（applyFreeBase 口径同生物属性底）。
        /// 取代旧「免费点全投耐久=入场+N 堆叠」（DurabilityBonusOf 同批退役）。</summary>
        public static int FreeDurabilityBaseOf(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Enchantment) return 0;
            float unit = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig.DurabilityUnitCost;
            if (unit <= 0f) return card.Durability;
            return (int)Math.Floor(SurplusOf(card) / unit);
        }

        /// <summary>结界入场耐久总层数（单源，2026-10-17 定案：结界一律走耐久经济）：
        /// 印刷耐久（下限 1——结界必有耐久池；2026-11-04 碑族0费改版：免费点改抵卡价，
        /// 旧「印刷+免费点+N 堆叠」退役）；非结界返回印刷值（装备=纯数据驱动）。
        /// 消费：EquipRules/MorphSystem 入场初始化 + 各显示口（卡面耐久/头顶配色基准）。</summary>
        public static int TotalDurabilityOf(CardData card)
        {
            if (card == null) return 0;
            if (card.Supertype != Cardtype.Enchantment) return card.Durability;
            return Math.Max(1, card.Durability);
        }

        /// <summary>结构费全额（费率 × 结构项，**无预算抵扣**）——衍生物口径专用
        ///（ContentPriceOf 组成部分：衍生物没走「入组 1 + 抽牌 2」，吃不到 3 点底盘）。
        /// 口径 = 攻/守/槽全算（分配改版不影响衍生物——无底盘即无免费点，三项照旧全额）。</summary>
        public static int StructureFeeFull(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            bool isCreature = card != null && card.Supertype == Cardtype.Creature;
            int items = (isCreature && card.AttackAbility ? 1 : 0) + (isCreature && card.GuardAbility ? 1 : 0)
                + CostDerivationService.CountEffectSlots(card);
            return items * rate;
        }

        /// <summary>
        /// 抵价逐点扣桶（原 ApplyChassisRefund 默认算法复活；RefundColor 自标已随字段删除退役）：
        /// 先扣灰桶（≥1 才扣），灰不足（法术常无灰分量）逐点从最高费用色桶扣（并列取枚举序靠前者）。
        /// 全桶空则停——抵价后可为 0（2026-10-10 放宽：免费地板=抵价耗尽即可，非「内容为 0 才免费」）。
        /// 2026-11-03 底盘改版配套：桶值浮点化（属性费真实小数后 mounted 恒 float，扣减仍逐整数点）。
        /// </summary>
        public static void ReduceBuckets(Dictionary<ManaType, float> mounted, int points)
        {
            for (int i = 0; i < points; i++)
            {
                if (mounted.TryGetValue(ManaType.Gray, out var g) && g >= 1f)
                {
                    mounted[ManaType.Gray] = g - 1f;
                    continue;
                }
                ManaType best = default;
                float bestV = 0f;
                foreach (var kv in mounted)
                    if (kv.Key != ManaType.Gray && kv.Value > bestV) { bestV = kv.Value; best = kv.Key; }
                if (bestV <= 0f) break;
                mounted[best] = bestV - 1f;
            }
        }

        /// <summary>代价抵扣逐点扣桶·色锁版（2026-10-31 代价槽改版定案）：只在代价原子**可产出色**
        ///（allowed=引用行 ManaList 非零色）内扣——扣序同 <see cref="ReduceBuckets"/>（先灰后最高费用色，
        /// 并列取枚举序靠前者），每色下限 0、溢出丢弃、不穿底到他色。allowed 空=无抵扣面（分文不扣）。
        /// 返回实际扣减点数：生成面超出可抵色容量即失效——「单个代价至多将这张卡对应色费用完全清零」
        /// 由下限 0+不溢出他色两条共同保证。消费口：CardCostService.ApplyPayloadDeduction 单源。
        /// 2026-11-03 底盘改版配套：桶值浮点化（扣减仍逐整数点，≥1 阈值 1e-4 容差）。</summary>
        public static int ReduceBucketsLocked(Dictionary<ManaType, float> mounted, int points, HashSet<ManaType> allowed)
        {
            if (allowed == null || allowed.Count == 0) return 0;
            int applied = 0;
            for (int i = 0; i < points; i++)
            {
                if (allowed.Contains(ManaType.Gray)
                    && mounted.TryGetValue(ManaType.Gray, out var g) && g >= 1f - 1e-4f)
                {
                    mounted[ManaType.Gray] = Math.Max(0f, g - 1f);
                    applied++;
                    continue;
                }
                ManaType best = default;
                float bestV = 0f;
                foreach (var kv in mounted)
                    if (allowed.Contains(kv.Key) && kv.Key != ManaType.Gray && kv.Value > bestV)
                    {
                        bestV = kv.Value;
                        best = kv.Key;
                    }
                if (bestV <= 1e-4f) break;
                mounted[best] = Math.Max(0f, bestV - 1f);
                applied++;
            }
            return applied;
        }

        // 抉择价差溢价已废（2026-09-21）——抉择分支计槽 + 地牌按所选模式产元素承担防套利。
        // 底盘退费（ApplyChassisRefund）/瞬间法术豁免（IsInstantSpell）/SurplusToSpeed 计价豁免
        // 已删（2026-10-10 可支配底盘定案）；2026-10-15 卡型默认分配定案再退役：
        // 结界二选一（ActivationFeeOffsetOf 发动费抵扣 / SurplusToDurability 字段——EffectExecutionEngine
        // 抵扣块与合成器开关同批拆除），改固定拆分「速1+其余入耐久」。
    }
}
